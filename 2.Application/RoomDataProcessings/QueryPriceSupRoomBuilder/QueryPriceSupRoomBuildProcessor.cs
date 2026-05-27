using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace QueryPriceSupRoomBuilder;

public sealed class QueryPriceSupRoomBuildProcessor
{
    readonly IQueryPriceClient _queryPriceClient;
    readonly ISupRoomRedisPublisher _publisher;
    readonly SupplierQueryPriceRateLimiter _rateLimiter;
    readonly SupRoomCandidateSelector _selector;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<QueryPriceSupRoomBuildProcessor> _logger;

    public QueryPriceSupRoomBuildProcessor(
        IQueryPriceClient queryPriceClient,
        ISupRoomRedisPublisher publisher,
        SupplierQueryPriceRateLimiter rateLimiter,
        SupRoomCandidateSelector selector,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<QueryPriceSupRoomBuildProcessor> logger)
    {
        _queryPriceClient = queryPriceClient;
        _publisher = publisher;
        _rateLimiter = rateLimiter;
        _selector = selector;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<ProcessTaskResult> ProcessAsync(
        QueryPriceSupRoomBuildTaskModel task,
        CancellationToken cancellationToken)
    {
        var results = await ProcessBatchAsync([task], null, cancellationToken);
        return results[task.Id];
    }

    public async Task<IReadOnlyDictionary<int, ProcessTaskResult>> ProcessBatchAsync(
        IReadOnlyList<QueryPriceSupRoomBuildTaskModel> tasks,
        Func<QueryPriceSupRoomBuildTaskModel, ProcessTaskResult, CancellationToken, ValueTask>? onTaskResult,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var suppliers = NormalizeSuppliers(options.Suppliers);
        var persons = NormalizePersons(options.Persons);
        var queryDimensions = (
            from rule in RoomBuildTaskParser.ParseDateWindows(options.DateWindows)
            from window in rule.Build(DateTime.Now.Date)
            from person in persons
            from supId in suppliers
            select new QueryPriceDimension(window.CheckIn, window.CheckOut, person, supId, rule.StayNights))
            .OrderBy(x => x.SupId == 51 ? 0 : 1)
            .ThenBy(x => x.SupId)
            .ToArray();

        var results = new Dictionary<int, ProcessTaskResult>();
        var pendingTasks = tasks
            .Where(x => !string.IsNullOrWhiteSpace(x.MTHotelCode))
            .GroupBy(x => x.MTHotelCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var totalTaskCount = tasks.Count;
        var completedTaskCount = results.Count;
        var totalHotelCount = pendingTasks.Count;

        _metrics.SetOuterProgress(totalTaskCount, completedTaskCount);
        _metrics.SetInnerProgress(totalHotelCount, 0);
        _metrics.SetDimensionLoopProgress(queryDimensions.Length, 0, 0);
        _metrics.SetHotelLoopProgress(totalHotelCount, 0, 0);

        _logger.LogInformation(
            "Room build query price batch started. TaskCount={TaskCount}, HotelCount={HotelCount}, DimensionCount={DimensionCount}, OuterTotalTasks={OuterTotalTasks}, OuterCompletedTasks={OuterCompletedTasks}, OuterRemainingTasks={OuterRemainingTasks}",
            tasks.Count,
            pendingTasks.Count,
            queryDimensions.Length,
            totalTaskCount,
            completedTaskCount,
            Math.Max(0, totalTaskCount - completedTaskCount));

        for (var dimensionIndex = 0; dimensionIndex < queryDimensions.Length; dimensionIndex++)
        {
            var dimension = queryDimensions[dimensionIndex];
            cancellationToken.ThrowIfCancellationRequested();
            if (pendingTasks.Count == 0)
            {
                completedTaskCount = results.Count;
                _metrics.SetOuterProgress(totalTaskCount, completedTaskCount);
                _metrics.SetInnerProgress(totalHotelCount, totalHotelCount);
                _metrics.SetDimensionLoopProgress(queryDimensions.Length, dimensionIndex, 0);
                _metrics.SetHotelLoopProgress(totalHotelCount, totalHotelCount, 0);
                _logger.LogInformation(
                    "Room build query price batch completed early. CompletedDimensionIndex={DimensionIndex}, TotalDimensions={TotalDimensions}, OuterTotalTasks={OuterTotalTasks}, OuterCompletedTasks={OuterCompletedTasks}, OuterRemainingTasks={OuterRemainingTasks}",
                    dimensionIndex,
                    queryDimensions.Length,
                    totalTaskCount,
                    completedTaskCount,
                    Math.Max(0, totalTaskCount - completedTaskCount));
                return await CompleteNoHitResultsAsync(tasks, results, onTaskResult, cancellationToken);
            }

            var hitHotelCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dimensionHotelTotal = pendingTasks.Count;
            var completedHotelCount = 0;
            var noPriceHotelCount = 0;
            var errorHotelCount = 0;
            var batchSize = ResolveBatchSize(options, dimension.SupId);
            var maxConcurrency = ResolveMaxConcurrency(options, dimension.SupId);
            _metrics.SetDimensionLoopProgress(queryDimensions.Length, dimensionIndex + 1, 1);
            _metrics.SetHotelLoopProgress(dimensionHotelTotal, 0, 0);

            _logger.LogInformation(
                "Room build query price dimension started. DimensionIndex={DimensionIndex}, TotalDimensions={TotalDimensions}, SupId={SupId}, AdultNumber={AdultNumber}, StayNights={StayNights}, CheckIn={CheckIn:yyyy-MM-dd}, CheckOut={CheckOut:yyyy-MM-dd}, TotalHotels={TotalHotels}, BatchSize={BatchSize}, MaxConcurrency={MaxConcurrency}, InnerTotalHotels={InnerTotalHotels}, InnerCompletedHotels={InnerCompletedHotels}, InnerRemainingHotels={InnerRemainingHotels}",
                dimensionIndex + 1,
                queryDimensions.Length,
                dimension.SupId,
                dimension.AdultNumber,
                dimension.StayNights,
                dimension.CheckIn,
                dimension.CheckOut,
                dimensionHotelTotal,
                batchSize,
                maxConcurrency,
                dimensionHotelTotal,
                completedHotelCount,
                Math.Max(0, dimensionHotelTotal - completedHotelCount));

            var runningTasks = new List<Task<QueryPriceBatchResult>>(maxConcurrency);
            foreach (var hotelCodes in pendingTasks.Keys.ToArray().Chunk(batchSize).Select(x => x.ToArray()))
            {
                using var lease = await _rateLimiter.WaitAsync(dimension.SupId, cancellationToken);
                runningTasks.Add(QueryHotelBatchAsync(dimensionIndex, queryDimensions.Length, dimension, hotelCodes, cancellationToken));
                _metrics.SetHotelLoopProgress(dimensionHotelTotal, completedHotelCount, runningTasks.Count);
                if (runningTasks.Count >= maxConcurrency)
                {
                    var batchResult = await ApplyCompletedQueryAsync(runningTasks, pendingTasks, results, hitHotelCodes, onTaskResult, cancellationToken);
                    completedHotelCount += batchResult.Result.HotelCount;
                    noPriceHotelCount += batchResult.Stats.NoPriceHotelCount;
                    errorHotelCount += batchResult.Stats.ErrorHotelCount;
                    completedTaskCount = results.Count;
                    _metrics.SetOuterProgress(totalTaskCount, completedTaskCount);
                    _metrics.SetInnerProgress(dimensionHotelTotal, completedHotelCount);
                    _metrics.SetHotelLoopProgress(dimensionHotelTotal, completedHotelCount, runningTasks.Count);
                    LogDimensionProgress(dimensionIndex, queryDimensions.Length, dimension, dimensionHotelTotal, completedHotelCount, noPriceHotelCount, errorHotelCount, hitHotelCodes.Count, runningTasks.Count);
                }
            }

            while (runningTasks.Count > 0)
            {
                var batchResult = await ApplyCompletedQueryAsync(runningTasks, pendingTasks, results, hitHotelCodes, onTaskResult, cancellationToken);
                completedHotelCount += batchResult.Result.HotelCount;
                noPriceHotelCount += batchResult.Stats.NoPriceHotelCount;
                errorHotelCount += batchResult.Stats.ErrorHotelCount;
                completedTaskCount = results.Count;
                _metrics.SetOuterProgress(totalTaskCount, completedTaskCount);
                _metrics.SetInnerProgress(dimensionHotelTotal, completedHotelCount);
                _metrics.SetHotelLoopProgress(dimensionHotelTotal, completedHotelCount, runningTasks.Count);
                LogDimensionProgress(dimensionIndex, queryDimensions.Length, dimension, dimensionHotelTotal, completedHotelCount, noPriceHotelCount, errorHotelCount, hitHotelCodes.Count, runningTasks.Count);
            }

            foreach (var hotelCode in hitHotelCodes)
            {
                pendingTasks.Remove(hotelCode);
            }

            _logger.LogInformation(
                "Room build query price dimension completed. DimensionIndex={DimensionIndex}, TotalDimensions={TotalDimensions}, SupId={SupId}, TotalHotels={TotalHotels}, CompletedHotels={CompletedHotels}, HitHotels={HitHotels}, NoPriceHotels={NoPriceHotels}, ErrorHotels={ErrorHotels}, RemainingHotels={RemainingHotels}, OuterTotalTasks={OuterTotalTasks}, OuterCompletedTasks={OuterCompletedTasks}, OuterRemainingTasks={OuterRemainingTasks}",
                dimensionIndex + 1,
                queryDimensions.Length,
                dimension.SupId,
                dimensionHotelTotal,
                completedHotelCount,
                hitHotelCodes.Count,
                noPriceHotelCount,
                errorHotelCount,
                pendingTasks.Count,
                totalTaskCount,
                completedTaskCount,
                Math.Max(0, totalTaskCount - completedTaskCount));
            _metrics.SetDimensionLoopProgress(queryDimensions.Length, dimensionIndex + 1, 0);
            _metrics.SetHotelLoopProgress(dimensionHotelTotal, completedHotelCount, 0);
        }

        var finalResults = await CompleteNoHitResultsAsync(tasks, results, onTaskResult, cancellationToken);
        _metrics.SetOuterProgress(totalTaskCount, finalResults.Count);
        _metrics.SetInnerProgress(totalHotelCount, totalHotelCount);
        _metrics.SetDimensionLoopProgress(queryDimensions.Length, queryDimensions.Length, 0);
        _metrics.SetHotelLoopProgress(totalHotelCount, totalHotelCount, 0);
        return finalResults;
    }

    async Task<QueryPriceBatchResult> QueryHotelBatchAsync(
        int dimensionIndex,
        int totalDimensions,
        QueryPriceDimension dimension,
        IReadOnlyList<string> hotelCodes,
        CancellationToken cancellationToken)
    {
        var response = await _queryPriceClient.QueryPriceAsync(
            new QueryPriceRequest(
                hotelCodes,
                dimension.SupId.ToString(),
                dimension.AdultNumber,
                0,
                dimension.CheckIn,
                dimension.CheckOut),
            cancellationToken);

        if (response is null)
        {
            _metrics.RecordError("query_price_request", "null_response");
            _metrics.RecordQueryPriceErrorCode(dimension.SupId, "null_response");
            _logger.LogWarning(
                "Room build query price request returned null response. DimensionIndex={DimensionIndex}, TotalDimensions={TotalDimensions}, SupId={SupId}, HotelCount={HotelCount}",
                dimensionIndex + 1,
                totalDimensions,
                dimension.SupId,
                hotelCodes.Count);
            return new QueryPriceBatchResult(new(hotelCodes.Count, []), new(0, hotelCodes.Count));
        }

        if (!IsSuccessCode(response.Code))
        {
            _metrics.RecordError("query_price_request", "error_code");
            _metrics.RecordQueryPriceErrorCode(dimension.SupId, response.Code.ToString());
            _logger.LogWarning(
                "Room build query price returned business error. SupId={SupId}, Code={Code}, Msg={Msg}, HotelCount={HotelCount}",
                dimension.SupId,
                response.Code,
                response.Msg,
                hotelCodes.Count);
            return new QueryPriceBatchResult(new(hotelCodes.Count, []), new(0, hotelCodes.Count));
        }

        if (response?.ListPrice.Count > 0 != true)
        {
            _metrics.RecordNoPrice(dimension.SupId, hotelCodes.Count);
            return new QueryPriceBatchResult(new(hotelCodes.Count, []), new(hotelCodes.Count, 0));
        }

        var hits = new List<QueryPriceBatchHit>();
        foreach (var priceGroup in response.ListPrice
            .Where(x => !string.IsNullOrWhiteSpace(x.MTHotelCode))
            .GroupBy(x => x.MTHotelCode!, StringComparer.OrdinalIgnoreCase))
        {
            var rooms = _selector.Select(priceGroup, dimension.SupId, _metrics);
            if (rooms.Count == 0)
            {
                continue;
            }

            var queuedCount = await _publisher.PushAsync(rooms, cancellationToken);
            _metrics.RecordPriceHit(dimension.SupId, rooms.Count);
            hits.Add(new QueryPriceBatchHit(priceGroup.Key, (int)queuedCount));
        }

        var noPriceHotelCount = Math.Max(0, hotelCodes.Count - hits.Count);
        if (noPriceHotelCount > 0)
        {
            _metrics.RecordNoPrice(dimension.SupId, noPriceHotelCount);
        }

        return new QueryPriceBatchResult(new(hotelCodes.Count, hits), new(noPriceHotelCount, 0));
    }

    static async Task<QueryPriceBatchResult> ApplyCompletedQueryAsync(
        List<Task<QueryPriceBatchResult>> runningTasks,
        IReadOnlyDictionary<string, QueryPriceSupRoomBuildTaskModel[]> pendingTasks,
        Dictionary<int, ProcessTaskResult> results,
        HashSet<string> hitHotelCodes,
        Func<QueryPriceSupRoomBuildTaskModel, ProcessTaskResult, CancellationToken, ValueTask>? onTaskResult,
        CancellationToken cancellationToken)
    {
        var completedTask = await Task.WhenAny(runningTasks);
        runningTasks.Remove(completedTask);
        var batchResult = await completedTask;

        foreach (var hit in batchResult.Result.Hits)
        {
            if (!pendingTasks.TryGetValue(hit.MTHotelCode, out var hotelTasks))
            {
                continue;
            }

            foreach (var task in hotelTasks)
            {
                if (results.ContainsKey(task.Id))
                {
                    continue;
                }

                var result = ProcessTaskResult.Completed(hit.QueuedCount);
                results[task.Id] = result;
                if (onTaskResult is not null)
                {
                    await onTaskResult(task, result, cancellationToken);
                }
            }

            hitHotelCodes.Add(hit.MTHotelCode);
        }

        return batchResult;
    }

    void LogDimensionProgress(
        int dimensionIndex,
        int totalDimensions,
        QueryPriceDimension dimension,
        int totalHotels,
        int completedHotels,
        int noPriceHotels,
        int errorHotels,
        int hitHotels,
        int runningBatchCount)
    {
        _logger.LogInformation(
            "Room build query price dimension progress. DimensionIndex={DimensionIndex}, TotalDimensions={TotalDimensions}, SupId={SupId}, AdultNumber={AdultNumber}, StayNights={StayNights}, CheckIn={CheckIn:yyyy-MM-dd}, CheckOut={CheckOut:yyyy-MM-dd}, TotalHotels={TotalHotels}, CompletedHotels={CompletedHotels}, RemainingHotels={RemainingHotels}, HitHotels={HitHotels}, NoPriceHotels={NoPriceHotels}, ErrorHotels={ErrorHotels}, RunningBatches={RunningBatches}",
            dimensionIndex + 1,
            totalDimensions,
            dimension.SupId,
            dimension.AdultNumber,
            dimension.StayNights,
            dimension.CheckIn,
            dimension.CheckOut,
            totalHotels,
            completedHotels,
            Math.Max(0, totalHotels - completedHotels),
            hitHotels,
            noPriceHotels,
            errorHotels,
            runningBatchCount);
    }

    static async Task<IReadOnlyDictionary<int, ProcessTaskResult>> CompleteNoHitResultsAsync(
        IReadOnlyList<QueryPriceSupRoomBuildTaskModel> tasks,
        Dictionary<int, ProcessTaskResult> results,
        Func<QueryPriceSupRoomBuildTaskModel, ProcessTaskResult, CancellationToken, ValueTask>? onTaskResult,
        CancellationToken cancellationToken)
    {
        foreach (var task in tasks)
        {
            if (results.ContainsKey(task.Id))
            {
                continue;
            }

            var result = ProcessTaskResult.NoHit();
            results[task.Id] = result;
            if (onTaskResult is not null)
            {
                await onTaskResult(task, result, cancellationToken);
            }
        }

        return results;
    }

    static int[] NormalizeSuppliers(int[] suppliers)
    {
        var normalized = suppliers.Where(x => x > 0).Distinct().ToArray();
        return normalized.Length == 0 ? [51] : normalized;
    }

    static int[] NormalizePersons(int[] persons)
    {
        var normalized = persons.Where(x => x > 0).Distinct().ToArray();
        return normalized.Length == 0 ? [2] : normalized;
    }

    static int ResolveBatchSize(RoomBuildTaskOptions options, int supId)
    {
        var batchSize = options.SupConcurrencyOverrides.TryGetValue(supId, out var supplierOptions)
            ? supplierOptions.BatchSize
            : options.DefaultBatchSize;

        return Math.Max(1, batchSize);
    }

    static int ResolveMaxConcurrency(RoomBuildTaskOptions options, int supId)
    {
        var maxConcurrency = options.SupConcurrencyOverrides.TryGetValue(supId, out var supplierOptions)
            ? supplierOptions.MaxConcurrency
            : options.DefaultMaxConcurrency;

        return Math.Max(1, maxConcurrency);
    }

    static bool IsSuccessCode(int? code)
    {
        return code is null or 0 or 1;
    }

    readonly record struct QueryPriceDimension(
        DateTime CheckIn,
        DateTime CheckOut,
        int AdultNumber,
        int SupId,
        int StayNights);

    readonly record struct QueryPriceBatchHit(string MTHotelCode, int QueuedCount);

    readonly record struct QueryPriceBatchResult(QueryPriceBatchResultCore Result, QueryPriceBatchStats Stats);

    readonly record struct QueryPriceBatchResultCore(int HotelCount, IReadOnlyList<QueryPriceBatchHit> Hits);

    readonly record struct QueryPriceBatchStats(int NoPriceHotelCount, int ErrorHotelCount);
}

public sealed record ProcessTaskResult(bool IsCompleted, int QueuedCount)
{
    public static ProcessTaskResult Completed(int queuedCount)
    {
        return new ProcessTaskResult(true, queuedCount);
    }

    public static ProcessTaskResult NoHit()
    {
        return new ProcessTaskResult(false, 0);
    }
}

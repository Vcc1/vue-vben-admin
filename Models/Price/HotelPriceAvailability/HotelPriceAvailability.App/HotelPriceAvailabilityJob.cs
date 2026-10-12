using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using HotelPriceAvailability.App.Configuration;
using HotelPriceAvailability.App.Contracts;
using HotelPriceAvailability.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelPriceAvailability.App;

public sealed class HotelPriceAvailabilityJob
{
    private readonly IPriceAvailabilityTaskRepository _taskRepository;
    private readonly IPriceQueryGateway _queryGateway;
    private readonly IOptions<HotelPriceAvailabilityOptions> _options;
    private readonly HotelPriceAvailabilityMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HotelPriceAvailabilityJob> _logger;

    public HotelPriceAvailabilityJob(
        IPriceAvailabilityTaskRepository taskRepository,
        IPriceQueryGateway queryGateway,
        IOptions<HotelPriceAvailabilityOptions> options,
        HotelPriceAvailabilityMetrics metrics,
        TimeProvider timeProvider,
        ILogger<HotelPriceAvailabilityJob> logger)
    {
        _taskRepository = taskRepository;
        _queryGateway = queryGateway;
        _options = options;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<JobExecutionSummary> ExecuteAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var options = _options.Value;
        var tasks = await _taskRepository.LoadPendingAsync(options.BatchNo, cancellationToken);
        var pricedHits = new ConcurrentDictionary<int, PriceHit>();
        var queryableTasks = tasks.Where(static task => task.LookupMode is not null).ToArray();
        var invalidTasks = tasks.Count - queryableTasks.Length;

        if (invalidTasks > 0)
        {
            _logger.LogWarning("Skipped {InvalidTasks} tasks without MTHotelCode or SHotelCode.", invalidTasks);
        }

        long totalRequests = 0;
        long successfulRequests = 0;
        long failedRequests = 0;
        var rules = options.DateWindows
            .Select(static item => new PriceWindowRule(item.StayDays, item.FromDay, item.ToDay))
            .ToArray();
        var combinations = PriceSearchPlan.Create(
            DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime),
            rules,
            options.Persons);

        foreach (var supplierGroup in queryableTasks.GroupBy(static task => task.SupId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var settings = GetSupplierSettings(options, supplierGroup.Key);
            var counters = await ProcessSupplierAsync(
                supplierGroup.ToArray(),
                combinations,
                settings,
                pricedHits,
                cancellationToken);

            totalRequests += counters.TotalRequests;
            successfulRequests += counters.SuccessfulRequests;
            failedRequests += counters.FailedRequests;
        }

        var hitList = pricedHits.Values.ToArray();
        if (hitList.Length > 0)
        {
            await _taskRepository.MarkPricedAsync(hitList, cancellationToken);
            _metrics.RecordPricedTasks(hitList.Length);
        }

        stopwatch.Stop();
        _metrics.RecordJobDuration(stopwatch.Elapsed);
        var summary = new JobExecutionSummary(
            tasks.Count,
            queryableTasks.Length,
            hitList.Length,
            queryableTasks.Length - hitList.Length,
            invalidTasks,
            totalRequests,
            successfulRequests,
            failedRequests,
            stopwatch.Elapsed);

        _logger.LogInformation(
            "Hotel price availability finished. BatchNo={BatchNo}, TotalTasks={TotalTasks}, PricedTasks={PricedTasks}, UnpricedTasks={UnpricedTasks}, Requests={Requests}, FailedRequests={FailedRequests}, ElapsedMs={ElapsedMs}",
            options.BatchNo,
            summary.TotalTasks,
            summary.PricedTasks,
            summary.UnpricedTasks,
            summary.TotalRequests,
            summary.FailedRequests,
            summary.Elapsed.TotalMilliseconds);
        return summary;
    }

    private async Task<RequestCounters> ProcessSupplierAsync(
        IReadOnlyList<PriceCheckTask> supplierTasks,
        IReadOnlyList<PriceSearchCombination> combinations,
        SupplierExecutionSettings settings,
        ConcurrentDictionary<int, PriceHit> pricedHits,
        CancellationToken cancellationToken)
    {
        var queue = Channel.CreateBounded<PriceQueryBatch>(new BoundedChannelOptions(
            Math.Max(2, settings.MaxConcurrency * 2))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });
        using var rateLimiter = CreateRateLimiter(supplierTasks[0].SupId, settings.RequestsPerSecond);
        long totalRequests = 0;
        long successfulRequests = 0;
        long failedRequests = 0;

        async Task ProduceAsync()
        {
            try
            {
                foreach (var combination in combinations)
                {
                    foreach (var mode in new[] { PriceLookupMode.MappedHotel, PriceLookupMode.SupplierHotel })
                    {
                        var modeTasks = supplierTasks
                            .Where(task => task.LookupMode == mode)
                            .ToArray();
                        if (modeTasks.Length == 0)
                        {
                            continue;
                        }

                        foreach (var chunk in modeTasks.Chunk(settings.BatchSize))
                        {
                            await queue.Writer.WriteAsync(
                                new PriceQueryBatch(supplierTasks[0].SupId, mode, chunk, combination),
                                cancellationToken);
                        }
                    }
                }
            }
            finally
            {
                queue.Writer.TryComplete();
            }
        }

        async Task ConsumeAsync()
        {
            await foreach (var batch in queue.Reader.ReadAllAsync(cancellationToken))
            {
                var pending = batch.Tasks
                    .Where(task => !pricedHits.ContainsKey(task.Id))
                    .ToArray();
                if (pending.Length == 0)
                {
                    continue;
                }

                RateLimitLease? lease = null;
                try
                {
                    if (rateLimiter is not null)
                    {
                        lease = await rateLimiter.AcquireAsync(batch.SupId, 1, cancellationToken);
                        if (!lease.IsAcquired)
                        {
                            Interlocked.Increment(ref failedRequests);
                            _logger.LogWarning("Rate limit queue rejected request for SupId={SupId}.", batch.SupId);
                            continue;
                        }
                    }

                    Interlocked.Increment(ref totalRequests);
                    var outcome = await _queryGateway.QueryAsync(
                        batch with { Tasks = pending },
                        cancellationToken);
                    _metrics.RecordRequest(batch.SupId, outcome.IsSuccessful);
                    if (!outcome.IsSuccessful)
                    {
                        Interlocked.Increment(ref failedRequests);
                        _logger.LogWarning(
                            "Price request failed. SupId={SupId}, Mode={Mode}, CheckIn={CheckIn}, CheckOut={CheckOut}, Error={Error}",
                            batch.SupId,
                            batch.LookupMode,
                            batch.Combination.Window.CheckIn,
                            batch.Combination.Window.CheckOut,
                            outcome.Error);
                        continue;
                    }

                    Interlocked.Increment(ref successfulRequests);
                    var validIds = pending.Select(static task => task.Id).ToHashSet();
                    foreach (var taskId in outcome.HitTaskIds.Where(validIds.Contains).Distinct())
                    {
                        var candidate = new PriceHit(taskId, batch.Combination);
                        pricedHits.AddOrUpdate(
                            taskId,
                            candidate,
                            (_, current) => PriceHit.SelectPreferred(current, candidate));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failedRequests);
                    _logger.LogError(ex, "Unexpected price query failure for SupId={SupId}.", batch.SupId);
                }
                finally
                {
                    lease?.Dispose();
                }
            }
        }

        var producer = ProduceAsync();
        var consumers = Enumerable.Range(0, settings.MaxConcurrency)
            .Select(_ => ConsumeAsync())
            .ToArray();
        await Task.WhenAll(consumers.Append(producer));
        return new RequestCounters(totalRequests, successfulRequests, failedRequests);
    }

    private static SupplierExecutionSettings GetSupplierSettings(
        HotelPriceAvailabilityOptions options,
        int supId)
    {
        var overrideOptions = options.SupplierOverrides.FirstOrDefault(item => item.SupId == supId);
        return overrideOptions is null
            ? new SupplierExecutionSettings(options.MaxConcurrency, options.BatchSize, 0)
            : new SupplierExecutionSettings(
                overrideOptions.MaxConcurrency,
                overrideOptions.BatchSize,
                overrideOptions.RequestsPerSecond);
    }

    private static PartitionedRateLimiter<int>? CreateRateLimiter(int supId, int requestsPerSecond)
    {
        if (requestsPerSecond <= 0)
        {
            return null;
        }

        return PartitionedRateLimiter.Create<int, int>(_ =>
            RateLimitPartition.GetFixedWindowLimiter(
                supId,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = requestsPerSecond,
                    Window = TimeSpan.FromSeconds(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = Math.Max(100, requestsPerSecond * 4),
                    AutoReplenishment = true
                }));
    }

    private readonly record struct RequestCounters(
        long TotalRequests,
        long SuccessfulRequests,
        long FailedRequests);
}

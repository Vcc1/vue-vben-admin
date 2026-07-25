using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace HotelMatchingPipeline.App;

public sealed class HotelMatchingService
{
    const string AmbiguousCandidateLoggerCategory =
        "HotelMatchingPipeline.Matching.AmbiguousCandidates";
    readonly IHotelMatchPipelineRepository _pipelineRepository;
    readonly IHotelMatchingRepository _repository;
    readonly IHotelMatchDeliveryRepository _deliveryRepository;
    readonly IHotelMatchCache _matchCache;
    readonly IThirdPartyHotelMatchClient _deliveryClient;
    readonly HotelFactMatchEvaluator _factEvaluator;
    readonly IOptions<HotelMatchingOptions> _options;
    readonly ILogger<HotelMatchingService> _logger;
    readonly ILogger _ambiguousCandidateLogger;
    readonly HotelMatchingMetrics _metrics;
    readonly IServiceScopeFactory _scopeFactory;

    public HotelMatchingService(
        IHotelMatchPipelineRepository pipelineRepository,
        IHotelMatchingRepository repository,
        IHotelMatchDeliveryRepository deliveryRepository,
        IHotelMatchCache matchCache,
        IThirdPartyHotelMatchClient deliveryClient,
        HotelFactMatchEvaluator factEvaluator,
        IOptions<HotelMatchingOptions> options,
        ILogger<HotelMatchingService> logger,
        IServiceScopeFactory scopeFactory,
        HotelMatchingMetrics? metrics = null,
        ILoggerFactory? loggerFactory = null)
    {
        _pipelineRepository = pipelineRepository;
        _repository = repository;
        _deliveryRepository = deliveryRepository;
        _matchCache = matchCache;
        _deliveryClient = deliveryClient;
        _factEvaluator = factEvaluator;
        _options = options;
        _logger = logger;
        _ambiguousCandidateLogger =
            loggerFactory?.CreateLogger(AmbiguousCandidateLoggerCategory) ?? logger;
        _metrics = metrics ?? new HotelMatchingMetrics();
        _scopeFactory = scopeFactory;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var capacity = Math.Clamp(options.QueueCapacity, 1, HotelMatchingOptions.MaxQueueCapacity);
        var writeBatchSize = Math.Clamp(options.WriteBatchSize, 1, capacity);
        var informationParallelism = Math.Clamp(options.InformationParallelism, 1, 64);
        var idQueue = CreateChannel<int>(capacity, singleReader: false, singleWriter: true);
        var workQueue = CreateChannel<HotelMatchingWorkItem>(
            capacity,
            singleReader: true,
            singleWriter: informationParallelism == 1);
        var resultQueue = CreateChannel<HotelMatchEvaluation>(
            capacity,
            singleReader: true,
            singleWriter: true);
        var processed = 0;
        var matchedCandidates = 0;
        var stopwatch = Stopwatch.StartNew();
        _metrics.StartRound(
            capacity,
            options.RecallRadiusMeters,
            options.MatchDistanceMeters,
            options.MaxCandidates,
            writeBatchSize,
            informationParallelism);
        using var pipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pipelineToken = pipelineCancellation.Token;

        var idTask = LoadIdsAsync(
            idQueue.Writer,
            () => QueueCount(idQueue.Reader),
            pipelineCancellation,
            pipelineToken);
        var informationTask = LoadInformationAsync(
            idQueue.Reader,
            workQueue.Writer,
            () => QueueCount(workQueue.Reader),
            options,
            informationParallelism,
            pipelineCancellation,
            pipelineToken);
        var matchingTask = MatchAsync(
            workQueue.Reader,
            resultQueue.Writer,
            () => QueueCount(resultQueue.Reader),
            options.MatchDistanceMeters,
            pipelineCancellation,
            pipelineToken);
        var writerTask = WriteAsync(
            resultQueue.Reader,
            writeBatchSize,
            count => processed += count,
            count => matchedCandidates += count,
            pipelineCancellation,
            pipelineToken);

        try
        {
            await Task.WhenAll(idTask, informationTask, matchingTask, writerTask);
            _metrics.CompleteRound(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch
        {
            _metrics.FailRound(stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        _logger.LogInformation(
            "Hotel matching pipeline completed. Processed={Processed}, MatchedCandidates={MatchedCandidates}, QueueCapacity={QueueCapacity}, InformationParallelism={InformationParallelism}, MatchDistanceMeters={MatchDistanceMeters}",
            processed,
            matchedCandidates,
            capacity,
            informationParallelism,
            options.MatchDistanceMeters);
        return processed;
    }

    public async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RunOnceAsync(cancellationToken);
            await Task.Delay(_options.Value.IdleDelayMilliseconds, cancellationToken);
        }
    }

    async Task LoadIdsAsync(
        ChannelWriter<int> writer,
        Func<int> queueCount,
        CancellationTokenSource pipelineCancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _pipelineRepository.LoadPendingSupplierHotelIdsAsync(
                async (id, token) =>
                {
                    await writer.WriteAsync(id, token);
                    _metrics.EnqueueId();
                    _metrics.SetIdQueueDepth(queueCount());
                },
                cancellationToken);
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            _metrics.RecordIdFailure();
            pipelineCancellation.Cancel();
            writer.TryComplete(ex);
            throw;
        }
    }

    async Task LoadInformationAsync(
        ChannelReader<int> reader,
        ChannelWriter<HotelMatchingWorkItem> writer,
        Func<int> workQueueCount,
        HotelMatchingOptions options,
        int parallelism,
        CancellationTokenSource pipelineCancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            var workers = Enumerable.Range(0, parallelism)
                .Select(_ => LoadInformationWorkerAsync(
                    reader,
                    writer,
                    workQueueCount,
                    options,
                    cancellationToken))
                .ToArray();
            await Task.WhenAll(workers);
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            pipelineCancellation.Cancel();
            writer.TryComplete(ex);
            throw;
        }
    }

    async Task LoadInformationWorkerAsync(
        ChannelReader<int> reader,
        ChannelWriter<HotelMatchingWorkItem> writer,
        Func<int> workQueueCount,
        HotelMatchingOptions options,
        CancellationToken cancellationToken)
    {
        await foreach (var supHotelId in reader.ReadAllAsync(cancellationToken))
        {
            _metrics.DequeueId();
            _metrics.SetIdQueueDepth(QueueCount(reader));
            var retryDelay = Math.Max(
                1,
                options.InformationRetryDelayMilliseconds);
            var retry = 0;
            HotelMatchingWorkItem workItem;
            while (true)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    workItem = await LoadMatchingWorkItemInScopeAsync(
                        supHotelId,
                        options.RecallRadiusMeters,
                        options.MaxCandidates,
                        cancellationToken);
                    _metrics.RecordInformation(
                        stopwatch.Elapsed.TotalMilliseconds,
                        loaded: true);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    retry++;
                    _metrics.RecordInformationRetry(
                        supHotelId,
                        stopwatch.Elapsed.TotalMilliseconds,
                        retryDelay);
                    _logger.LogWarning(
                        ex,
                        "Hotel matching information load failed and will retry indefinitely. SupHotelId={SupHotelId}, Retry={Retry}, RetryDelayMilliseconds={RetryDelayMilliseconds}",
                        supHotelId,
                        retry,
                        retryDelay);
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
            await writer.WriteAsync(workItem, cancellationToken);
            _metrics.EnqueueWork();
            _metrics.SetWorkQueueDepth(workQueueCount());
        }
    }

    async Task<HotelMatchingWorkItem> LoadMatchingWorkItemInScopeAsync(
        int supHotelId,
        int recallRadiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IHotelMatchPipelineRepository>();
        return await repository.LoadMatchingWorkItemAsync(
            supHotelId,
            recallRadiusMeters,
            maxCandidates,
            cancellationToken);
    }

    async Task MatchAsync(
        ChannelReader<HotelMatchingWorkItem> reader,
        ChannelWriter<HotelMatchEvaluation> writer,
        Func<int> resultQueueCount,
        int maximumDistanceMeters,
        CancellationTokenSource pipelineCancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var workItem in reader.ReadAllAsync(cancellationToken))
            {
                _metrics.DequeueWork();
                _metrics.SetWorkQueueDepth(QueueCount(reader));
                var stopwatch = Stopwatch.StartNew();
                HotelMatchEvaluation evaluation;
                try
                {
                    evaluation = _factEvaluator.Evaluate(workItem, maximumDistanceMeters);
                }
                catch
                {
                    _metrics.RecordMatchFailure(stopwatch.Elapsed.TotalMilliseconds);
                    throw;
                }
                await writer.WriteAsync(evaluation, cancellationToken);
                _metrics.EnqueueResult();
                _metrics.SetResultQueueDepth(resultQueueCount());
                if (evaluation.HasConflict)
                {
                    _ambiguousCandidateLogger.LogInformation(
                        "Multiple OTA parent hotel candidates found. " +
                        "SupHotelId={SupHotelId}, CandidateCount={CandidateCount}, " +
                        "MTHotelCodes={MTHotelCodes}",
                        workItem.SupHotelId,
                        evaluation.Matches.Count,
                        string.Join(
                            ",",
                            evaluation.Matches.Select(match => match.MTHotelCode)));
                }
                _metrics.RecordMatch(
                    evaluation.Matches.Count,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            pipelineCancellation.Cancel();
            writer.TryComplete(ex);
            throw;
        }
    }

    async Task WriteAsync(
        ChannelReader<HotelMatchEvaluation> reader,
        int writeBatchSize,
        Action<int> addProcessed,
        Action<int> addMatchedCandidates,
        CancellationTokenSource pipelineCancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new List<HotelMatchEvaluation>(writeBatchSize);
            await foreach (var evaluation in reader.ReadAllAsync(cancellationToken))
            {
                _metrics.DequeueResult();
                _metrics.SetResultQueueDepth(QueueCount(reader));
                buffer.Add(evaluation);
                if (buffer.Count >= writeBatchSize)
                {
                    await FlushAsync(buffer, addProcessed, addMatchedCandidates, cancellationToken);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                await FlushAsync(buffer, addProcessed, addMatchedCandidates, cancellationToken);
            }
        }
        catch
        {
            pipelineCancellation.Cancel();
            throw;
        }
    }

    async Task FlushAsync(
        IReadOnlyCollection<HotelMatchEvaluation> evaluations,
        Action<int> addProcessed,
        Action<int> addMatchedCandidates,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var candidateCount = evaluations.Sum(evaluation => evaluation.Matches.Count);
        try
        {
            await _pipelineRepository.SaveMatchEvaluationsAsync(
                evaluations,
                _options.Value.Version,
                cancellationToken);
            // Redis matching output is temporarily disabled.
            // await _matchCache.SaveAsync(
            //     evaluations,
            //     _options.Value.Version,
            //     cancellationToken);
            _metrics.RecordWrite(
                evaluations.Count,
                candidateCount,
                stopwatch.Elapsed.TotalMilliseconds,
                succeeded: true);
        }
        catch
        {
            _metrics.RecordWrite(
                evaluations.Count,
                candidateCount,
                stopwatch.Elapsed.TotalMilliseconds,
                succeeded: false);
            throw;
        }
        addProcessed(evaluations.Count);
        addMatchedCandidates(candidateCount);
    }

    static Channel<T> CreateChannel<T>(
        int capacity,
        bool singleReader,
        bool singleWriter)
        => Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = singleReader,
            SingleWriter = singleWriter,
            AllowSynchronousContinuations = false
        });

    static int QueueCount<T>(ChannelReader<T> reader)
        => reader.CanCount ? reader.Count : 0;

    public async Task<HotelManualReviewResult> ConfirmAsync(
        HotelManualReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SHotelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MTHotelCode);
        var workItem = await _repository.GetSupplierHotelAsync(request.SupId, request.SHotelCode, cancellationToken);
        if (workItem is null)
        {
            return new HotelManualReviewResult(false, "notFound", "supplier_hotel_not_found");
        }

        if (!workItem.Hotel.IsQualityValid)
        {
            return new HotelManualReviewResult(false, "dataProblem", "supplier_quality_problem");
        }

        var ota = await _repository.GetOtaHotelAsync(request.MTHotelCode!, cancellationToken);
        if (ota is null || !ota.IsQualityValid)
        {
            return new HotelManualReviewResult(false, "invalidCandidate", "ota_hotel_not_found_or_invalid");
        }

        var receipt = await DeliverAsync(workItem, request.MTHotelCode!, _options.Value.Version, cancellationToken);
        var decision = new HotelMatchDecision(
            receipt.Success ? HotelMatchDecisionKind.AutoMatched : HotelMatchDecisionKind.NoMatch,
            receipt.Success ? request.MTHotelCode : null,
            [],
            ["manual_confirm"]);
        await _repository.CompleteMatchAsync(
            workItem,
            decision,
            _options.Value.Version,
            receipt.Error,
            receipt.Success ? null : DateTimeOffset.Now.AddMinutes(15),
            cancellationToken);
        return new HotelManualReviewResult(receipt.Success, receipt.Success ? "delivered" : "deliveryFailed", receipt.Error);
    }

    public async Task<HotelManualReviewResult> RejectAsync(
        HotelManualReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SHotelCode);
        var workItem = await _repository.GetSupplierHotelAsync(request.SupId, request.SHotelCode, cancellationToken);
        if (workItem is null)
        {
            return new HotelManualReviewResult(false, "notFound", "supplier_hotel_not_found");
        }

        await _repository.CompleteMatchAsync(
            workItem,
            new HotelMatchDecision(HotelMatchDecisionKind.NoMatch, null, [], ["manual_reject", request.Reason ?? string.Empty]),
            _options.Value.Version,
            null,
            null,
            cancellationToken);
        return new HotelManualReviewResult(true, "rejected", null);
    }

    async Task<HotelMatchDeliveryReceipt> DeliverAsync(
        HotelMatchWorkItem workItem,
        string mtHotelCode,
        int matchingVersion,
        CancellationToken cancellationToken)
    {
        var supId = workItem.Hotel.SupplierId ?? throw new InvalidOperationException("Supplier hotel is missing SupId.");
        var idempotencyKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{supId}\u001F{workItem.Hotel.HotelCode}\u001F{workItem.NormalizedDataHash}\u001F{matchingVersion}")));
        var command = new HotelMatchDeliveryCommand(
            supId,
            workItem.Hotel.HotelCode,
            mtHotelCode,
            workItem.SourceDataVersion,
            workItem.NormalizedDataHash,
            workItem.CleanVersion,
            matchingVersion,
            idempotencyKey);

        if (!await _deliveryRepository.TryCreateAsync(command, cancellationToken))
        {
            return new HotelMatchDeliveryReceipt(true, "already_recorded", null);
        }

        await _deliveryRepository.MarkSendingAsync(idempotencyKey, cancellationToken);
        var receipt = await _deliveryClient.DeliverAsync(command, cancellationToken);
        await _deliveryRepository.MarkCompletedAsync(
            idempotencyKey,
            receipt,
            receipt.Success ? null : DateTimeOffset.Now.AddMinutes(15),
            cancellationToken);
        return receipt;
    }
}

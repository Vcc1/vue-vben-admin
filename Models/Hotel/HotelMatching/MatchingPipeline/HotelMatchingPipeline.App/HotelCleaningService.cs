using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace HotelMatchingPipeline.App;

public sealed class HotelCleaningService
{
    readonly IHotelCleaningRepository _repository;
    readonly HotelCleaner _cleaner;
    readonly IOptions<HotelCleaningOptions> _options;
    readonly IHotelStarLevelIssueRecorder _starLevelIssueRecorder;
    readonly ILogger<HotelCleaningService> _logger;
    readonly HotelCleaningMetrics? _metrics;

    public HotelCleaningService(
        IHotelCleaningRepository repository,
        HotelCleaner cleaner,
        IOptions<HotelCleaningOptions> options,
        IHotelStarLevelIssueRecorder starLevelIssueRecorder,
        ILogger<HotelCleaningService> logger,
        HotelCleaningMetrics? metrics = null)
    {
        _repository = repository;
        _cleaner = cleaner;
        _options = options;
        _starLevelIssueRecorder = starLevelIssueRecorder;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var changedSince = DateTimeOffset.Now.AddHours(-options.ChangeWindowHours);
        var cleanedCount = 0;
        _metrics?.StartRun();
        var progressByType = new Dictionary<HotelDataType, HotelCleaningProgress>();
        foreach (var sourceType in new[] { HotelDataType.Platform, HotelDataType.Supplier })
        {
            var progress = await _repository.GetCleaningProgressAsync(
                sourceType,
                changedSince,
                options.Version,
                options.RegionFenceVersion,
                cancellationToken);
            progressByType[sourceType] = progress;
            LogProgress(sourceType, progress, batchSaved: 0, runSaved: cleanedCount);
        }

        var capacity = Math.Clamp(options.QueueCapacity, 1, 10_000);
        var rawQueue = Channel.CreateBounded<RawPipelineItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
        var cleanQueue = Channel.CreateBounded<CleanPipelineItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
        var metrics = new PipelineMetrics(rawQueue.Reader, cleanQueue.Reader, capacity);
        _metrics?.SetQueueDepths(metrics.RawQueueCurrent, capacity, metrics.CleanQueueCurrent, capacity);
        _logger.LogInformation(
            "Hotel cleaning pipeline started. RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}, CleanQueueCurrent={CleanQueueCurrent}, CleanQueueCapacity={CleanQueueCapacity}, ReadBatchSize={ReadBatchSize}, WriteBatchSize={WriteBatchSize}",
            metrics.RawQueueCurrent,
            capacity,
            metrics.CleanQueueCurrent,
            capacity,
            Math.Min(options.BatchSize, capacity),
            options.SaveBatchSize);
        using var pipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pipelineToken = pipelineCancellation.Token;

        var readerTask = RunStageAsync(
            () => ReadAsync(rawQueue.Writer, metrics, changedSince, options, pipelineToken),
            pipelineCancellation,
            rawQueue.Writer,
            cleanQueue.Writer);
        var cleanerTask = RunStageAsync(
            () => CleanAsync(rawQueue.Reader, cleanQueue.Writer, metrics, options, pipelineToken),
            pipelineCancellation,
            rawQueue.Writer,
            cleanQueue.Writer);
        var writerTask = RunStageAsync(
            () => WriteAsync(cleanQueue.Reader, metrics, options, progressByType, () => cleanedCount, value => cleanedCount = value, pipelineToken),
            pipelineCancellation,
            rawQueue.Writer,
            cleanQueue.Writer);

        try
        {
            await Task.WhenAll(readerTask, cleanerTask, writerTask);
            _metrics?.SetNodeCompleted(metrics.ReadCompleted, metrics.CleanCompleted, metrics.WriteCompleted);
            _metrics?.CompleteRun();
        }
        catch
        {
            _metrics?.SetNodeCompleted(metrics.ReadCompleted, metrics.CleanCompleted, metrics.WriteCompleted);
            _metrics?.FailRun();
            pipelineCancellation.Cancel();
            rawQueue.Writer.TryComplete();
            cleanQueue.Writer.TryComplete();
            try
            {
                await Task.WhenAll(readerTask, cleanerTask, writerTask);
            }
            catch
            {
                // Preserve the original stage exception.
            }

            throw;
        }

        return cleanedCount;
    }

    async Task ReadAsync(
        ChannelWriter<RawPipelineItem> writer,
        PipelineMetrics metrics,
        DateTimeOffset changedSince,
        HotelCleaningOptions options,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<PipelineKey>();
        var exhausted = new HashSet<HotelDataType>();
        var sourceTypes = new[] { HotelDataType.Platform, HotelDataType.Supplier };

        try
        {
            while (exhausted.Count < sourceTypes.Length)
            {
                var madeProgress = false;
                foreach (var type in sourceTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (exhausted.Contains(type))
                    {
                        continue;
                    }

                    var rawHotels = await _repository.GetHotelsToCleanAsync(
                        type,
                        changedSince,
                        options.Version,
                        options.RegionFenceVersion,
                        Math.Min(options.BatchSize, options.QueueCapacity),
                        cancellationToken);
                    if (rawHotels.Count == 0)
                    {
                        exhausted.Add(type);
                        continue;
                    }

                    var batch = new List<RawPipelineItem>(rawHotels.Count);
                    foreach (var raw in rawHotels)
                    {
                        var key = PipelineKey.Create(raw);
                        if (!seen.Add(key))
                        {
                            continue;
                        }

                        var item = new RawPipelineItem(key, raw);
                        batch.Add(item);
                    }

                    if (batch.Count > 0)
                    {
                        madeProgress = true;
                        batch[^1].IsBatchEnd = true;
                        foreach (var item in batch)
                        {
                            await writer.WriteAsync(item, cancellationToken);
                        }
                        Interlocked.Add(ref metrics.ReadCompleted, batch.Count);
                        Interlocked.Add(ref metrics.ReadDeduplicated, rawHotels.Count - batch.Count);
                        _metrics?.AddReader(
                            batch.Count,
                            rawHotels.Count - batch.Count,
                            type == HotelDataType.Platform ? "OTA" : "SUP");
                        RecordQueueMetrics(metrics);

                        _logger.LogInformation(
                            "Hotel cleaning read batch completed. Source={Source}, Read={Read}, Enqueued={Enqueued}, Deduplicated={Deduplicated}, NodeCompleted={NodeCompleted}, RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}, CleanQueueCurrent={CleanQueueCurrent}, CleanQueueCapacity={CleanQueueCapacity}",
                            type == HotelDataType.Platform ? "OTA" : "SUP",
                            rawHotels.Count,
                            batch.Count,
                            rawHotels.Count - batch.Count,
                            metrics.ReadCompleted,
                            metrics.RawQueueCurrent,
                            metrics.Capacity,
                            metrics.CleanQueueCurrent,
                            metrics.Capacity);

                        await Task.WhenAll(batch.Select(item => item.Completed.Task)).WaitAsync(cancellationToken);
                        foreach (var item in batch)
                        {
                            seen.Remove(item.Key);
                        }
                    }
                }

                if (!madeProgress && exhausted.Count < sourceTypes.Length)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
                }
            }

            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
            throw;
        }
    }

    async Task CleanAsync(
        ChannelReader<RawPipelineItem> reader,
        ChannelWriter<CleanPipelineItem> writer,
        PipelineMetrics metrics,
        HotelCleaningOptions options,
        CancellationToken cancellationToken)
    {
        var batchProcessed = 0;
        var batchSucceeded = 0;
        var batchFailed = 0;
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                batchProcessed++;
                if (TryClean(item.Raw, options.Version, options.RegionFenceVersion, cancellationToken, out var result, out var failure))
                {
                    batchSucceeded++;
                    Interlocked.Increment(ref metrics.CleanSucceeded);
                    await writer.WriteAsync(new CleanPipelineItem(item, result, null), cancellationToken);
                }
                else
                {
                    batchFailed++;
                    Interlocked.Increment(ref metrics.CleanFailed);
                    await writer.WriteAsync(new CleanPipelineItem(item, null, failure), cancellationToken);
                }
                Interlocked.Increment(ref metrics.CleanCompleted);

                if (item.IsBatchEnd)
                {
                    _metrics?.AddCleaner(
                        batchProcessed,
                        batchSucceeded,
                        batchFailed,
                        item.Raw.Type == HotelDataType.Platform ? "OTA" : "SUP");
                    RecordQueueMetrics(metrics);
                    _logger.LogInformation(
                        "Hotel cleaning clean batch completed. Source={Source}, Processed={Processed}, Succeeded={Succeeded}, Failed={Failed}, NodeCompleted={NodeCompleted}, RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}, CleanQueueCurrent={CleanQueueCurrent}, CleanQueueCapacity={CleanQueueCapacity}",
                        item.Raw.Type == HotelDataType.Platform ? "OTA" : "SUP",
                        batchProcessed,
                        batchSucceeded,
                        batchFailed,
                        metrics.CleanCompleted,
                        metrics.RawQueueCurrent,
                        metrics.Capacity,
                        metrics.CleanQueueCurrent,
                        metrics.Capacity);
                    batchProcessed = 0;
                    batchSucceeded = 0;
                    batchFailed = 0;
                }
            }

            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
            throw;
        }
    }

    async Task WriteAsync(
        ChannelReader<CleanPipelineItem> reader,
        PipelineMetrics metrics,
        HotelCleaningOptions options,
        IDictionary<HotelDataType, HotelCleaningProgress> progressByType,
        Func<int> getCleanedCount,
        Action<int> setCleanedCount,
        CancellationToken cancellationToken)
    {
        var buffer = new List<CleanPipelineItem>(options.SaveBatchSize);
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                buffer.Add(item);
                if (buffer.Count >= options.SaveBatchSize || item.Source.IsBatchEnd)
                {
                    await FlushWriteBatchAsync(buffer, metrics, options, progressByType, getCleanedCount, setCleanedCount, cancellationToken);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                await FlushWriteBatchAsync(buffer, metrics, options, progressByType, getCleanedCount, setCleanedCount, cancellationToken);
            }
        }
        catch
        {
            foreach (var item in buffer)
            {
                item.Source.Completed.TrySetCanceled(cancellationToken);
            }

            throw;
        }
    }

    async Task FlushWriteBatchAsync(
        IReadOnlyCollection<CleanPipelineItem> batch,
        PipelineMetrics metrics,
        HotelCleaningOptions options,
        IDictionary<HotelDataType, HotelCleaningProgress> progressByType,
        Func<int> getCleanedCount,
        Action<int> setCleanedCount,
        CancellationToken cancellationToken)
    {
        var results = batch.Where(x => x.Result is not null).Select(x => x.Result!).ToArray();
        var failures = batch.Where(x => x.Failure is not null).Select(x => x.Failure!).ToArray();
        try
        {
            await _starLevelIssueRecorder.FlushAsync(cancellationToken);
            await _repository.RecordCleaningFailuresAsync(failures, cancellationToken);
            var savedCount = results.Length == 0
                ? 0
                : await _repository.SaveCleanedHotelsAsync(results, cancellationToken);
            Interlocked.Add(ref metrics.WriteCompleted, batch.Count);
            Interlocked.Add(ref metrics.WriteSucceeded, savedCount + failures.Length);
            Interlocked.Add(ref metrics.WriteFailed, Math.Max(0, results.Length - savedCount));
            var runSaved = getCleanedCount() + savedCount;
            setCleanedCount(runSaved);

            foreach (var typeGroup in batch.GroupBy(x => x.Source.Raw.Type))
            {
                var type = typeGroup.Key;
                var groupResults = typeGroup.Where(x => x.Result is not null).Select(x => x.Result!).ToArray();
                var groupFailures = typeGroup.Count(x => x.Failure is not null);
                var savedForType = results.Count(x => x.Raw.Type == type);
                if (savedCount != results.Length)
                {
                    // Repository may quarantine individual rows after a batch write failure.
                    savedForType = Math.Min(savedForType, savedCount);
                }

                var failureCount = typeGroup.Count(x => x.Failure is not null);
                var previous = progressByType[type];
                var updated = previous with
                {
                    Cleaned = Math.Min(previous.Total, previous.Cleaned + savedForType),
                    Failed = Math.Min(previous.Total, previous.Failed + failureCount + Math.Max(0, typeGroup.Count(x => x.Result is not null) - savedForType))
                };
                progressByType[type] = updated;
                _metrics?.AddWriter(
                    typeGroup.Count(),
                    savedForType + groupFailures,
                    Math.Max(0, groupResults.Length - savedForType),
                    type == HotelDataType.Platform ? "OTA" : "SUP");
                RecordQueueMetrics(metrics);
                _logger.LogInformation(
                    "Hotel cleaning write batch completed. Source={Source}, Read={Read}, Cleaned={Cleaned}, Failed={Failed}, QueueBatch={QueueBatch}, RunSaved={RunSaved}, NodeCompleted={NodeCompleted}, RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}, CleanQueueCurrent={CleanQueueCurrent}, CleanQueueCapacity={CleanQueueCapacity}",
                    type == HotelDataType.Platform ? "OTA" : "SUP",
                    typeGroup.Count(),
                    savedForType,
                    failureCount + Math.Max(0, typeGroup.Count(x => x.Result is not null) - savedForType),
                    batch.Count,
                    runSaved,
                    metrics.WriteCompleted,
                    metrics.RawQueueCurrent,
                    metrics.Capacity,
                    metrics.CleanQueueCurrent,
                    metrics.Capacity);
                LogProgress(type, updated, savedForType, runSaved);
            }

            foreach (var item in batch)
            {
                item.Source.Completed.TrySetResult(true);
            }
        }
        catch
        {
            foreach (var item in batch)
            {
                item.Source.Completed.TrySetCanceled(cancellationToken);
            }

            throw;
        }
    }

    static async Task RunStageAsync(
        Func<Task> stage,
        CancellationTokenSource cancellation,
        ChannelWriter<RawPipelineItem> rawWriter,
        ChannelWriter<CleanPipelineItem> cleanWriter)
    {
        try
        {
            await stage();
        }
        catch
        {
            cancellation.Cancel();
            rawWriter.TryComplete();
            cleanWriter.TryComplete();
            throw;
        }
    }

    readonly record struct PipelineKey(HotelDataType Type, string Value)
    {
        public static PipelineKey Create(RawHotelData raw)
        {
            var dataId = raw.Id ?? raw.PlatformHotelId;
            var value = dataId is > 0
                ? dataId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Concat(
                    raw.SupplierId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                    ":",
                    raw.HotelCode.Trim().ToUpperInvariant());
            return new PipelineKey(raw.Type, value);
        }
    }

    sealed class PipelineMetrics
    {
        readonly ChannelReader<RawPipelineItem> _rawQueue;
        readonly ChannelReader<CleanPipelineItem> _cleanQueue;

        public PipelineMetrics(
            ChannelReader<RawPipelineItem> rawQueue,
            ChannelReader<CleanPipelineItem> cleanQueue,
            int capacity)
        {
            _rawQueue = rawQueue;
            _cleanQueue = cleanQueue;
            Capacity = capacity;
        }

        public int Capacity { get; }

        public long ReadCompleted;
        public long ReadDeduplicated;
        public long CleanCompleted;
        public long CleanSucceeded;
        public long CleanFailed;
        public long WriteCompleted;
        public long WriteSucceeded;
        public long WriteFailed;

        public int RawQueueCurrent => _rawQueue.CanCount ? _rawQueue.Count : -1;

        public int CleanQueueCurrent => _cleanQueue.CanCount ? _cleanQueue.Count : -1;
    }

    sealed class RawPipelineItem
    {
        public RawPipelineItem(PipelineKey key, RawHotelData raw)
        {
            Key = key;
            Raw = raw;
        }

        public PipelineKey Key { get; }

        public RawHotelData Raw { get; }

        public bool IsBatchEnd { get; set; }

        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    sealed record CleanPipelineItem(
        RawPipelineItem Source,
        HotelCleaningResult? Result,
        HotelCleaningFailureRecord? Failure);

    void LogProgress(HotelDataType type, HotelCleaningProgress progress, int batchSaved, int runSaved)
    {
        _metrics?.SetSourceProgress(
            type == HotelDataType.Platform ? "OTA" : "SUP",
            progress.Total,
            progress.Cleaned,
            progress.Failed);
        _logger.LogInformation(
            "Hotel cleaning progress. Source={Source}, Total={Total}, Cleaned={Cleaned}, Remaining={Remaining}, Failed={Failed}, Pending={Pending}, BatchSaved={BatchSaved}, RunSaved={RunSaved}",
            type == HotelDataType.Platform ? "OTA" : "SUP",
            progress.Total,
            progress.Cleaned,
            progress.Remaining,
            progress.Failed,
            progress.Pending,
            batchSaved,
            runSaved);
    }

    void RecordQueueMetrics(PipelineMetrics metrics)
    {
        _metrics?.SetQueueDepths(
            metrics.RawQueueCurrent,
            metrics.Capacity,
            metrics.CleanQueueCurrent,
            metrics.Capacity);
    }

    bool TryClean(
        RawHotelData raw,
        int cleanVersion,
        int regionBoundaryVersion,
        CancellationToken cancellationToken,
        out HotelCleaningResult? result,
        out HotelCleaningFailureRecord? failure)
    {
        try
        {
            result = _cleaner.Clean(raw, cleanVersion, cancellationToken);
            failure = null;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Hotel cleaning failed. Type={Type}, HotelCode={HotelCode}, SupId={SupId}", raw.Type, raw.HotelCode, raw.SupplierId);
            result = null;
            failure = new HotelCleaningFailureRecord(
                raw.Type,
                raw.Id ?? raw.PlatformHotelId ?? 0,
                raw.SupplierId,
                raw.SourceSystemId,
                raw.HotelCode,
                ex.GetType().FullName ?? ex.GetType().Name,
                ex.Message,
                ex.ToString(),
                cleanVersion,
                regionBoundaryVersion);
            return false;
        }
    }
}

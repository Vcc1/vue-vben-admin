using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        var saveBatchSize = Math.Clamp(options.SaveBatchSize, 1, HotelCleaningOptions.MaxSaveBatchSize);
        var cleanedCount = 0;
        _metrics?.StartRun();
        var progressByType = new ConcurrentDictionary<HotelDataType, HotelCleaningProgress>(
            new[]
            {
                new KeyValuePair<HotelDataType, HotelCleaningProgress>(HotelDataType.Platform, new(0, 0, 0)),
                new KeyValuePair<HotelDataType, HotelCleaningProgress>(HotelDataType.Supplier, new(0, 0, 0))
            });

        var capacity = Math.Clamp(options.QueueCapacity, 1, 1_000_000);
        var refillThreshold = Math.Clamp(options.QueueRefillThreshold, 0, capacity);
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
        var idQueue = Channel.CreateUnbounded<HotelIdItem>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
        var metrics = new PipelineMetrics(rawQueue.Reader, cleanQueue.Reader, capacity);
        RecordQueueMetrics(metrics, Math.Min(options.BatchSize, capacity));
        _metrics?.SetRefillThreshold(refillThreshold);
        _logger.LogInformation(
            "Hotel cleaning pipeline started. IdQueueCurrent={IdQueueCurrent}, IdQueueCapacity=Unbounded, RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}, CleanQueueCurrent={CleanQueueCurrent}, CleanQueueCapacity={CleanQueueCapacity}, ReadBatchSize={ReadBatchSize}, WriteBatchSize={WriteBatchSize}, RefillThreshold={RefillThreshold}, RefillDelaySeconds={RefillDelaySeconds}",
            metrics.IdQueueCurrent,
            metrics.RawQueueCurrent,
            capacity,
            metrics.CleanQueueCurrent,
            capacity,
            Math.Min(options.BatchSize, capacity),
            saveBatchSize,
            refillThreshold,
            options.QueueRefillDelaySeconds);
        using var pipelineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pipelineToken = pipelineCancellation.Token;

        var idLoaderTask = LoadIdsAsync(
            idQueue.Writer,
            metrics,
            progressByType,
            changedSince,
            options,
            pipelineToken);
        var readerTask = RunStageAsync(
            () => LoadDetailsAsync(idQueue.Reader, rawQueue.Writer, metrics, options, pipelineToken),
            pipelineCancellation,
            rawQueue.Writer,
            cleanQueue.Writer);
        var cleanerTask = RunStageAsync(
            () => CleanAsync(rawQueue.Reader, cleanQueue.Writer, metrics, options, pipelineToken),
            pipelineCancellation,
            rawQueue.Writer,
            cleanQueue.Writer);
        var writerTask = RunStageAsync(
            () => WriteAsync(cleanQueue.Reader, metrics, saveBatchSize, progressByType, () => cleanedCount, value => cleanedCount = value, pipelineToken),
            pipelineCancellation,
            rawQueue.Writer,
            cleanQueue.Writer);

        try
        {
            await Task.WhenAll(idLoaderTask, readerTask, cleanerTask, writerTask);
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
                await Task.WhenAll(idLoaderTask, readerTask, cleanerTask, writerTask);
            }
            catch
            {
                // Preserve the original stage exception.
            }

            throw;
        }

        return cleanedCount;
    }

    async Task LoadIdsAsync(
        ChannelWriter<HotelIdItem> writer,
        PipelineMetrics metrics,
        ConcurrentDictionary<HotelDataType, HotelCleaningProgress> progressByType,
        DateTimeOffset changedSince,
        HotelCleaningOptions options,
        CancellationToken cancellationToken)
    {
        var sourceTypes = new[] { HotelDataType.Platform, HotelDataType.Supplier };
        var batchSize = Math.Clamp(options.BatchSize, 1, metrics.Capacity);

        try
        {
            foreach (var type in sourceTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var workSetClock = Stopwatch.StartNew();
                var source = SourceName(type);
                _metrics?.StartIdSnapshot(source);
                _logger.LogInformation(
                    "Hotel cleaning pending ID snapshot loading started. Source={Source}",
                    SourceName(type));
                var queuedCount = 0;
                HotelCleaningWorkSet workSet;
                try
                {
                    workSet = await _repository.LoadCleaningWorkSetAsync(
                        type,
                        changedSince,
                        options.Version,
                        options.RegionFenceVersion,
                        batchSize,
                        scan =>
                        {
                            _metrics?.SetSourceTotal(source, scan.Total);
                            _metrics?.ReportIdSnapshotProgress(
                                source,
                                scan.Total,
                                scan.Pending,
                                scan.Cleaned,
                                scan.Failed,
                                workSetClock.Elapsed.TotalMilliseconds);
                            progressByType.AddOrUpdate(
                                type,
                                new HotelCleaningProgress(scan.Total, scan.Cleaned, scan.Failed),
                                (_, current) => current with { Total = Math.Max(current.Total, scan.Total) });
                        },
                        async (dataId, token) =>
                        {
                            await writer.WriteAsync(
                                new HotelIdItem(type, dataId, queuedCount),
                                token);
                            metrics.IdEnqueued();
                            queuedCount++;
                            if (queuedCount % 256 == 0)
                            {
                                RecordQueueMetrics(metrics, batchSize);
                            }
                        },
                        cancellationToken);
                }
                catch
                {
                    _metrics?.FailIdSnapshot(source, workSetClock.Elapsed.TotalMilliseconds);
                    throw;
                }

                _metrics?.CompleteIdSnapshot(
                    source,
                    workSet.Total,
                    workSet.Cleaned,
                    workSet.Failed,
                    queuedCount,
                    workSetClock.Elapsed.TotalMilliseconds);
                var progress = progressByType.AddOrUpdate(
                    type,
                    workSet.Progress,
                    (_, runProgress) => new HotelCleaningProgress(
                        workSet.Total,
                        Math.Min(workSet.Total, workSet.Cleaned + runProgress.Cleaned),
                        Math.Min(workSet.Total, workSet.Failed + runProgress.Failed)));
                LogProgress(type, progress, batchSaved: 0, runSaved: 0);
                _logger.LogInformation(
                    "Hotel cleaning pending ID snapshot loading completed. Source={Source}, Total={Total}, Pending={Pending}, Cleaned={Cleaned}, Failed={Failed}, ElapsedMs={ElapsedMs}",
                    SourceName(type),
                    workSet.Total,
                    queuedCount,
                    workSet.Cleaned,
                    workSet.Failed,
                    workSetClock.ElapsedMilliseconds);
                await writer.WriteAsync(HotelIdItem.SourceEnd(type, queuedCount), cancellationToken);
                RecordQueueMetrics(metrics, batchSize);
                if (queuedCount < batchSize)
                {
                    await WaitForLowIdRefillAsync(metrics, options, queuedCount, cancellationToken);
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

    async Task LoadDetailsAsync(
        ChannelReader<HotelIdItem> reader,
        ChannelWriter<RawPipelineItem> writer,
        PipelineMetrics metrics,
        HotelCleaningOptions options,
        CancellationToken cancellationToken)
    {
        var loadedBatchCount = 0;
        HotelIdItem? carry = null;

        try
        {
            while (carry is not null || await reader.WaitToReadAsync(cancellationToken))
            {
                HotelIdItem first;
                if (carry is { } carried)
                {
                    first = carried;
                    carry = null;
                }
                else if (!reader.TryRead(out first))
                {
                    continue;
                }
                if (first.IsSourceEnd)
                {
                    continue;
                }
                metrics.IdDequeued();

                var requestedIds = new List<int>(Math.Min(options.BatchSize, metrics.Capacity))
                {
                    first.DataId
                };
                var last = first;
                var sourceEnded = false;
                while (requestedIds.Count < Math.Min(options.BatchSize, metrics.Capacity)
                    && await reader.WaitToReadAsync(cancellationToken))
                {
                    while (requestedIds.Count < Math.Min(options.BatchSize, metrics.Capacity)
                        && reader.TryRead(out var next))
                    {
                        if (next.IsSourceEnd)
                        {
                            sourceEnded = true;
                            break;
                        }
                        if (next.Type != first.Type)
                        {
                            carry = next;
                            break;
                        }

                        requestedIds.Add(next.DataId);
                        metrics.IdDequeued();
                        last = next;
                    }

                    if (carry is not null || sourceEnded)
                    {
                        break;
                    }
                }
                RecordQueueMetrics(metrics, Math.Min(options.BatchSize, metrics.Capacity));

                var detailClock = Stopwatch.StartNew();
                IReadOnlyList<RawHotelData> rawHotels;
                try
                {
                    rawHotels = await _repository.GetHotelsByIdsAsync(first.Type, requestedIds, cancellationToken);
                }
                catch
                {
                    _metrics?.RecordDetailQueryFailure(
                        SourceName(first.Type),
                        requestedIds.Count,
                        detailClock.Elapsed.TotalMilliseconds);
                    throw;
                }
                loadedBatchCount++;
                var missing = Math.Max(0, requestedIds.Count - rawHotels.Count);
                _metrics?.RecordDetailBatch(
                    SourceName(first.Type),
                    requestedIds.Count,
                    rawHotels.Count,
                    missing,
                    detailClock.Elapsed.TotalMilliseconds);
                if (rawHotels.Count == 0)
                {
                    continue;
                }

                var batch = rawHotels.Select(static raw => new RawPipelineItem(raw)).ToArray();
                batch[^1].IsBatchEnd = true;
                foreach (var item in batch)
                {
                    await writer.WriteAsync(item, cancellationToken);
                }

                Interlocked.Add(ref metrics.ReadCompleted, rawHotels.Count);
                Interlocked.Add(ref metrics.ReadDeduplicated, missing);
                _metrics?.AddReader(rawHotels.Count, missing, SourceName(first.Type));
                RecordQueueMetrics(metrics);
                _logger.LogInformation(
                    "Hotel cleaning detail batch loaded. Source={Source}, Batch={Batch}, Requested={Requested}, Enqueued={Enqueued}, Missing={Missing}, PendingOffset={PendingOffset}, ElapsedMs={ElapsedMs}, RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}",
                    SourceName(first.Type),
                    loadedBatchCount,
                    requestedIds.Count,
                    rawHotels.Count,
                    missing,
                    last.Offset + 1,
                    detailClock.ElapsedMilliseconds,
                    metrics.RawQueueCurrent,
                    metrics.Capacity);
            }

            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
            throw;
        }
    }

    async Task WaitForLowIdRefillAsync(
        PipelineMetrics metrics,
        HotelCleaningOptions options,
        int refillCount,
        CancellationToken cancellationToken)
    {
        var refillThreshold = Math.Clamp(options.QueueRefillThreshold, 0, metrics.Capacity);
        var refillDelay = TimeSpan.FromSeconds(Math.Max(0, options.QueueRefillDelaySeconds));
        var waitClock = Stopwatch.StartNew();
        try
        {
            if (refillThreshold > 0 && metrics.IdQueueCurrent >= refillThreshold)
            {
                _metrics?.SetRefillState(1);
            }

            while (refillThreshold > 0 && metrics.IdQueueCurrent >= refillThreshold)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }

            if (refillDelay > TimeSpan.Zero)
            {
                _metrics?.SetRefillState(2);
                _logger.LogInformation(
                    "Hotel cleaning ID refill returned less than one full batch; ID loading is cooling down. RefillCount={RefillCount}, IdQueueCurrent={IdQueueCurrent}, RefillThreshold={RefillThreshold}, DelaySeconds={DelaySeconds}",
                    refillCount,
                    metrics.IdQueueCurrent,
                    refillThreshold,
                    refillDelay.TotalSeconds);
                await Task.Delay(refillDelay, cancellationToken);
            }

            _metrics?.RecordRefillWait(waitClock.Elapsed.TotalMilliseconds);
            _logger.LogInformation(
                "Hotel cleaning ID low-refill cooldown completed. RefillCount={RefillCount}, IdQueueCurrent={IdQueueCurrent}, RefillThreshold={RefillThreshold}, WaitedMs={WaitedMs}",
                refillCount,
                metrics.IdQueueCurrent,
                refillThreshold,
                waitClock.ElapsedMilliseconds);
        }
        finally
        {
            _metrics?.SetRefillState(0);
        }
    }

    async Task CleanAsync(
        ChannelReader<RawPipelineItem> reader,
        ChannelWriter<CleanPipelineItem> writer,
        PipelineMetrics metrics,
        HotelCleaningOptions options,
        CancellationToken cancellationToken)
    {
        var parallelism = Math.Clamp(options.CleanParallelism, 1, 64);
        var runningTasks = new List<Task<CleanPipelineItem>>(parallelism);
        _metrics?.SetCleanTaskState(0, parallelism);
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                runningTasks.Add(Task.Run(
                    () => CleanItem(
                        item,
                        options.Version,
                        options.RegionFenceVersion,
                        cancellationToken),
                    cancellationToken));
                _metrics?.SetCleanTaskState(runningTasks.Count, parallelism);

                if (runningTasks.Count >= parallelism)
                {
                    await DrainOneCleanTaskAsync(
                        runningTasks,
                        writer,
                        metrics,
                        parallelism,
                        cancellationToken);
                }
            }

            while (runningTasks.Count > 0)
            {
                await DrainOneCleanTaskAsync(
                    runningTasks,
                    writer,
                    metrics,
                    parallelism,
                    cancellationToken);
            }

            writer.TryComplete();
        }
        catch (Exception ex)
        {
            try
            {
                await Task.WhenAll(runningTasks);
            }
            catch
            {
                // Preserve the exception that stopped the cleaning stage.
            }

            writer.TryComplete(ex);
            throw;
        }
        finally
        {
            _metrics?.SetCleanTaskState(0, parallelism);
        }
    }

    CleanPipelineItem CleanItem(
        RawPipelineItem item,
        int cleanVersion,
        int regionFenceVersion,
        CancellationToken cancellationToken)
    {
        return TryClean(
            item.Raw,
            cleanVersion,
            regionFenceVersion,
            cancellationToken,
            out var result,
            out var failure)
            ? new CleanPipelineItem(item, result, null)
            : new CleanPipelineItem(item, null, failure);
    }

    async Task DrainOneCleanTaskAsync(
        List<Task<CleanPipelineItem>> runningTasks,
        ChannelWriter<CleanPipelineItem> writer,
        PipelineMetrics metrics,
        int parallelism,
        CancellationToken cancellationToken)
    {
        var completedTask = await Task.WhenAny(runningTasks);
        runningTasks.Remove(completedTask);
        _metrics?.SetCleanTaskState(runningTasks.Count, parallelism);
        var item = await completedTask;
        var succeeded = item.Result is not null;
        Interlocked.Increment(ref metrics.CleanCompleted);
        if (succeeded)
        {
            Interlocked.Increment(ref metrics.CleanSucceeded);
        }
        else
        {
            Interlocked.Increment(ref metrics.CleanFailed);
        }

        _metrics?.AddCleaner(
            1,
            succeeded ? 1 : 0,
            succeeded ? 0 : 1,
            SourceName(item.Source.Raw.Type));
        await writer.WriteAsync(item, cancellationToken);

        if (item.Source.IsBatchEnd)
        {
            RecordQueueMetrics(metrics);
            _logger.LogInformation(
                "Hotel cleaning detail-batch marker completed. Source={Source}, Parallelism={Parallelism}, RunningTasks={RunningTasks}, NodeCompleted={NodeCompleted}, RawQueueCurrent={RawQueueCurrent}, RawQueueCapacity={RawQueueCapacity}, CleanQueueCurrent={CleanQueueCurrent}, CleanQueueCapacity={CleanQueueCapacity}",
                SourceName(item.Source.Raw.Type),
                parallelism,
                runningTasks.Count,
                metrics.CleanCompleted,
                metrics.RawQueueCurrent,
                metrics.Capacity,
                metrics.CleanQueueCurrent,
                metrics.Capacity);
        }
    }

    async Task WriteAsync(
        ChannelReader<CleanPipelineItem> reader,
        PipelineMetrics metrics,
        int saveBatchSize,
        ConcurrentDictionary<HotelDataType, HotelCleaningProgress> progressByType,
        Func<int> getCleanedCount,
        Action<int> setCleanedCount,
        CancellationToken cancellationToken)
    {
        var buffer = new List<CleanPipelineItem>(saveBatchSize);
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                buffer.Add(item);
                if (buffer.Count >= saveBatchSize || item.Source.IsBatchEnd)
                {
                    await FlushWriteBatchAsync(buffer, metrics, progressByType, getCleanedCount, setCleanedCount, cancellationToken);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                await FlushWriteBatchAsync(buffer, metrics, progressByType, getCleanedCount, setCleanedCount, cancellationToken);
            }
        }
        catch
        {
            throw;
        }
    }

    async Task FlushWriteBatchAsync(
        IReadOnlyCollection<CleanPipelineItem> batch,
        PipelineMetrics metrics,
        ConcurrentDictionary<HotelDataType, HotelCleaningProgress> progressByType,
        Func<int> getCleanedCount,
        Action<int> setCleanedCount,
        CancellationToken cancellationToken)
    {
        var results = batch.Where(x => x.Result is not null).Select(x => x.Result!).ToArray();
        var failures = batch.Where(x => x.Failure is not null).Select(x => x.Failure!).ToArray();
        var writeClock = Stopwatch.StartNew();
        var writeSucceeded = false;
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
                var failedForType = failureCount + Math.Max(0, typeGroup.Count(x => x.Result is not null) - savedForType);
                var updated = progressByType.AddOrUpdate(
                    type,
                    new HotelCleaningProgress(0, savedForType, failedForType),
                    (_, previous) => previous with
                    {
                        Cleaned = previous.Total > 0
                            ? Math.Min(previous.Total, previous.Cleaned + savedForType)
                            : previous.Cleaned + savedForType,
                        Failed = previous.Total > 0
                            ? Math.Min(previous.Total, previous.Failed + failedForType)
                            : previous.Failed + failedForType
                    });
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

            writeSucceeded = true;
        }
        finally
        {
            _metrics?.RecordWriteBatch(
                batch.Count,
                writeClock.Elapsed.TotalMilliseconds,
                writeSucceeded);
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

    sealed class PipelineMetrics
    {
        readonly ChannelReader<RawPipelineItem> _rawQueue;
        readonly ChannelReader<CleanPipelineItem> _cleanQueue;
        long _idQueueCurrent;

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

        public long IdQueueCurrent => Math.Max(0, Interlocked.Read(ref _idQueueCurrent));

        public void IdEnqueued() => Interlocked.Increment(ref _idQueueCurrent);

        public void IdDequeued() => Interlocked.Decrement(ref _idQueueCurrent);

        public int RawQueueCurrent => _rawQueue.CanCount ? _rawQueue.Count : -1;

        public int CleanQueueCurrent => _cleanQueue.CanCount ? _cleanQueue.Count : -1;
    }

    sealed class RawPipelineItem
    {
        public RawPipelineItem(RawHotelData raw)
        {
            Raw = raw;
        }

        public RawHotelData Raw { get; }

        public bool IsBatchEnd { get; set; }
    }

    sealed record CleanPipelineItem(
        RawPipelineItem Source,
        HotelCleaningResult? Result,
        HotelCleaningFailureRecord? Failure);

    readonly record struct HotelIdItem(HotelDataType Type, int DataId, int Offset, bool IsSourceEnd = false)
    {
        public static HotelIdItem SourceEnd(HotelDataType type, int offset)
            => new(type, 0, offset, true);
    }

    static string SourceName(HotelDataType type)
        => type == HotelDataType.Platform ? "OTA" : "SUP";

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

    void RecordQueueMetrics(PipelineMetrics metrics, int? batchSize = null)
    {
        _metrics?.SetQueueDepths(
            metrics.IdQueueCurrent,
            0,
            batchSize ?? metrics.Capacity,
            metrics.RawQueueCurrent,
            metrics.Capacity,
            metrics.CleanQueueCurrent,
            metrics.Capacity);
        _metrics?.SetNodeCompleted(metrics.ReadCompleted, metrics.CleanCompleted, metrics.WriteCompleted);
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

using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTARoomBuilder.Contracts;
using System.Threading.Channels;

namespace OTARoomBuilder;

public sealed class OTARoomBuilderWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<OTARoomBuilderOptions> _options;
    readonly OTARoomBuilderMetrics _metrics;
    readonly ILogger<OTARoomBuilderWorker> _logger;
    readonly Channel<OTARoomBuildJob> _jobChannel = Channel.CreateUnbounded<OTARoomBuildJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    readonly Channel<HotelBuildQueueItem> _hotelBuildChannel = Channel.CreateUnbounded<HotelBuildQueueItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public OTARoomBuilderWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OTARoomBuilderOptions> options,
        OTARoomBuilderMetrics metrics,
        ILogger<OTARoomBuilderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ThreadPool.SetMinThreads(16, 16);
        _logger.LogInformation("OTARoomBuilder worker started. CpuCount={CpuCount}", Environment.ProcessorCount);

        var fullSyncProducer = RunFullSyncProducerAsync(stoppingToken);
        var incrementalProducer = RunIncrementalProducerAsync(stoppingToken);
        var hotelLoader = RunHotelLoaderAsync(stoppingToken);
        var matcher = RunMatcherAsync(stoppingToken);

        await Task.WhenAll(fullSyncProducer, incrementalProducer, hotelLoader, matcher);
    }

    async Task RunFullSyncProducerAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (options.RunFullSyncImmediatelyOnStartup)
        {
            await EnqueueAsync(OTARoomBuildJob.FullSync(), cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = GetNextDelay(options.GetFullSyncCron());
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            await EnqueueAsync(OTARoomBuildJob.FullSync(), cancellationToken);
            options = _options.CurrentValue;
        }
    }

    async Task RunIncrementalProducerAsync(CancellationToken cancellationToken)
    {
        var startupLookbackSeconds = Math.Max(0, _options.CurrentValue.IncrementalStartupLookbackSeconds);
        OtaRoomBuilderIncrementalCursor? cursor = new(DateTime.Now.AddSeconds(-startupLookbackSeconds), 0);

        while (!cancellationToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            if (!options.EnableIncrementalScan)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOtaRoomBuilderRepository>();
                var batch = await repository.LoadIncrementalMtHotelCodesAsync(cursor, options.IncrementalBatchSize, cancellationToken);
                cursor = batch.NextCursor ?? cursor;
                var distinctHotelCodes = batch.HotelCodes
                    .Where(static code => !string.IsNullOrWhiteSpace(code))
                    .Select(static code => code.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (distinctHotelCodes.Length > 0)
                {
                    _logger.LogInformation(
                        "Loaded incremental OTARoomBuilder task batch from SupRoomInfo. Count={Count}, LastUpdateDate={LastUpdateDate}, LastId={LastId}",
                        distinctHotelCodes.Length,
                        batch.NextCursor?.LastUpdateDate,
                        batch.NextCursor?.LastId);
                    await EnqueueAsync(OTARoomBuildJob.Incremental(distinctHotelCodes), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.RecordError("incremental_scan");
                _logger.LogError(ex, "OTARoomBuilder incremental scan failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.IncrementalPollIntervalSeconds)), cancellationToken);
        }
    }

    async Task RunHotelLoaderAsync(CancellationToken cancellationToken)
    {
        while (await _jobChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_jobChannel.Reader.TryRead(out var job))
            {
                await RunLoadStageSafelyAsync(job, cancellationToken);
            }
        }
    }

    async Task RunMatcherAsync(CancellationToken cancellationToken)
    {
        while (await _hotelBuildChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_hotelBuildChannel.Reader.TryRead(out var item))
            {
                await RunMatchStageSafelyAsync(item, cancellationToken);
            }
        }
    }

    async Task EnqueueAsync(OTARoomBuildJob job, CancellationToken cancellationToken)
    {
        await _jobChannel.Writer.WriteAsync(job, cancellationToken);
    }

    async Task RunLoadStageSafelyAsync(OTARoomBuildJob job, CancellationToken cancellationToken)
    {
        try
        {
            using var _ = _metrics.TrackRun();
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();
            var currentOptions = _options.CurrentValue;
            var hotelCodes = job.JobType == OTARoomBuildJobType.FullSync
                ? await processor.LoadFullSyncHotelCodesAsync(cancellationToken)
                : job.HotelCodes;
            int? lastDaysOffset = job.JobType == OTARoomBuildJobType.FullSync
                ? currentOptions.LastDaysOffset
                : null;
            var triggerSource = job.JobType == OTARoomBuildJobType.FullSync
                ? "full_sync"
                : "incremental_queue";

            _metrics.ResetRun();
            _metrics.SetPendingHotels(hotelCodes.Count);
            _logger.LogInformation(
                "OTARoomBuilder job started at {Now}. JobType={JobType}, HotelCount={HotelCount}, OutputMode={OutputMode}",
                DateTimeOffset.Now,
                job.JobType,
                hotelCodes.Count,
                currentOptions.OutputMode);
            _logger.LogInformation(
                "Loaded {HotelCount} hotel codes. TriggerSource={TriggerSource}, LastDaysOffset={LastDaysOffset}, OutputMode={OutputMode}",
                hotelCodes.Count,
                triggerSource,
                lastDaysOffset,
                currentOptions.OutputMode);

            foreach (var hotelChunk in hotelCodes.Chunk(Math.Max(1, currentOptions.TaskChunkSize)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batchContext = await processor.LoadHotelBuildBatchContextAsync(
                    hotelChunk,
                    lastDaysOffset,
                    currentOptions.OutputMode,
                    cancellationToken);
                await _hotelBuildChannel.Writer.WriteAsync(HotelBuildQueueItem.ForBatch(batchContext), cancellationToken);
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _hotelBuildChannel.Writer.WriteAsync(HotelBuildQueueItem.ForCompletion(completion), cancellationToken);
            await completion.Task.WaitAsync(cancellationToken);

            _logger.LogInformation(
                "OTARoomBuilder job completed at {Now}. JobType={JobType}, HotelCount={HotelCount}",
                DateTimeOffset.Now,
                job.JobType,
                hotelCodes.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _metrics.RecordError(job.JobType.ToString());
            _logger.LogError(ex, "OTARoomBuilder job failed. JobType={JobType}", job.JobType);
        }
    }

    async Task RunMatchStageSafelyAsync(HotelBuildQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.BatchContext is not null)
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();
                await processor.ProcessHotelBuildBatchContextAsync(item.BatchContext, cancellationToken);
                return;
            }

            item.Completion?.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            item.Completion?.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            item.Completion?.TrySetException(ex);
            _metrics.RecordError("match_stage");
            _logger.LogError(ex, "OTARoomBuilder match stage failed");
        }
    }

    static TimeSpan GetNextDelay(string cronText)
    {
        var cron = CronExpression.Parse(cronText, CronFormat.IncludeSeconds);
        var now = DateTimeOffset.Now;
        var next = cron.GetNextOccurrence(now, TimeZoneInfo.Local);
        return next is null ? TimeSpan.FromMinutes(10) : next.Value - now;
    }

    sealed record OTARoomBuildJob(OTARoomBuildJobType JobType, IReadOnlyList<string> HotelCodes)
    {
        public static OTARoomBuildJob FullSync() => new(OTARoomBuildJobType.FullSync, Array.Empty<string>());

        public static OTARoomBuildJob Incremental(IReadOnlyList<string> hotelCodes) => new(OTARoomBuildJobType.Incremental, hotelCodes);
    }

    enum OTARoomBuildJobType
    {
        FullSync = 1,
        Incremental = 2
    }

    sealed record HotelBuildQueueItem(HotelBuildBatchContext? BatchContext, TaskCompletionSource? Completion)
    {
        public static HotelBuildQueueItem ForBatch(HotelBuildBatchContext batchContext) => new(batchContext, null);

        public static HotelBuildQueueItem ForCompletion(TaskCompletionSource completion) => new(null, completion);
    }
}

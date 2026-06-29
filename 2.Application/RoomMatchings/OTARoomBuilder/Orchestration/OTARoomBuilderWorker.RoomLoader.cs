using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
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
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OTARoomBuilder.Contracts;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
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
                var batch = await repository.LoadIncrementalMtHotelCodesAsync(
                    cursor,
                    options.IncrementalBatchSize,
                    options.GetEnabledSupIds(),
                    cancellationToken);
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

    async Task EnqueueAsync(OTARoomBuildJob job, CancellationToken cancellationToken)
    {
        await _jobChannel.Writer.WriteAsync(job, cancellationToken);
        _metrics.RecordQueueEnqueued("job");
    }
}

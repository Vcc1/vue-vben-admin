using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
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

    async Task RunMatchStageSafelyAsync(HotelBuildQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.BatchContext is null)
            {
                if (item.Completion is not null)
                {
                    await _roomCreateChannel.Writer.WriteAsync(RoomCreateQueueItem.ForCompletion(item.Completion), cancellationToken);
                }
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();

            foreach (var hotel in item.BatchContext.Hotels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var createdRooms = await processor.BuildOtaRoomsForHotelAsync(hotel, cancellationToken);
                await _roomCreateChannel.Writer.WriteAsync(
                    RoomCreateQueueItem.ForBatch(createdRooms, item.BatchContext.OutputMode),
                    cancellationToken);
            }
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
}

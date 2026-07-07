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
            if (item.Hotel is null)
            {
                if (item.Completion is not null)
                {
                    await _roomCreateChannel.Writer.WriteAsync(RoomCreateQueueItem.ForCompletion(item.Completion), cancellationToken);
                }
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();

            cancellationToken.ThrowIfCancellationRequested();
            var createdRooms = await processor.BuildOtaRoomsForHotelAsync(item.Hotel, cancellationToken);
            await _roomCreateChannel.Writer.WriteAsync(
                RoomCreateQueueItem.ForHotel(createdRooms, item.OutputMode ?? string.Empty),
                cancellationToken);
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

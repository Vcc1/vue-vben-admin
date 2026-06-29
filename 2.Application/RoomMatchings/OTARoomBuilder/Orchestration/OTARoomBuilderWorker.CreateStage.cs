using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
    async Task RunCreatorAsync(CancellationToken cancellationToken)
    {
        while (await _roomCreateChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_roomCreateChannel.Reader.TryRead(out var item))
            {
                await RunCreateStageSafelyAsync(item, cancellationToken);
            }
        }
    }

    async Task RunCreateStageSafelyAsync(RoomCreateQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.Completion is not null)
            {
                item.Completion.TrySetResult();
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();

            var rooms = item.Rooms ?? [];
            if (rooms.Count == 0)
            {
                _metrics.RecordSkipped(1, "no_new_rooms");
                return;
            }

            await processor.PersistCreatedRoomsAsync(
                rooms,
                item.OutputMode ?? string.Empty,
                cancellationToken);
            _metrics.RecordCreated(rooms.Count, item.OutputMode ?? string.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            item.Completion?.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            item.Completion?.TrySetException(ex);
            _metrics.RecordError("create_stage");
            _logger.LogError(ex, "OTARoomBuilder create stage failed");
        }
        finally
        {
            if (item.Completion is null)
            {
                _metrics.DecrementPendingHotels();
            }
        }
    }
}

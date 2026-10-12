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
                _metrics.RecordQueueDequeued("room_create");
                await RunCreateStageSafelyAsync(item, cancellationToken);
            }
        }
    }

    async Task RunCreateStageSafelyAsync(RoomCreateQueueItem item, CancellationToken cancellationToken)
    {
        var isHotel = item.Rooms is not null;
        if (isHotel)
        {
            _metrics.RecordStageStarted("create");
        }

        try
        {
            if (item.Completion is not null)
            {
                if (_options.CurrentValue.EnableAiRoomBranch)
                {
                    await _aiRoomChannel.Writer.WriteAsync(
                        AiRoomQueueItem.ForCompletion(item.Completion),
                        cancellationToken);
                }
                else
                {
                    item.Completion.TrySetResult();
                }
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();

            var rooms = item.Rooms ?? [];
            IReadOnlySet<int> successfullyCreated = new HashSet<int>();
            if (rooms.Count > 0)
            {
                successfullyCreated = await processor.PersistCreatedRoomsWithResultsAsync(
                    rooms,
                    item.OutputMode ?? string.Empty,
                    cancellationToken);
                _metrics.RecordCreated(successfullyCreated.Count, item.OutputMode ?? string.Empty);
            }
            else
            {
                _metrics.RecordSkipped(1, "no_new_rooms");
            }

            if (_options.CurrentValue.EnableAiRoomBranch && item.Hotel is not null)
            {
                await _aiRoomChannel.Writer.WriteAsync(
                    AiRoomQueueItem.ForHotel(
                        item.Hotel,
                        successfullyCreated,
                        item.OutputMode ?? string.Empty),
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
            _metrics.RecordError("create_stage");
            _logger.LogError(ex, "OTARoomBuilder create stage failed");
        }
        finally
        {
            if (item.Completion is null && !_options.CurrentValue.EnableAiRoomBranch)
            {
                _metrics.DecrementPendingHotels();
            }

            if (isHotel)
            {
                _metrics.RecordStageCompleted("create");
                _metrics.RecordStageFinished("create");
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
    async Task RunAiRoomAsync(CancellationToken cancellationToken)
    {
        var inFlight = new HashSet<Task>();
        while (await _aiRoomChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_aiRoomChannel.Reader.TryRead(out var item))
            {
                if (item.Completion is not null)
                {
                    await DrainAiRoomTasksAsync(inFlight, cancellationToken, waitForAll: true);
                    item.Completion.TrySetResult();
                    continue;
                }

                var task = RunAiRoomStageSafelyAsync(item, cancellationToken);
                inFlight.Add(task);
                if (inFlight.Count >= Math.Clamp(_options.CurrentValue.AiRoomTaskConcurrency, 1, 32))
                {
                    await DrainAiRoomTasksAsync(inFlight, cancellationToken, waitForAll: false);
                }
            }
        }
    }

    async Task RunAiRoomStageSafelyAsync(AiRoomQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.Hotel is null)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();
            var result = await processor.BuildAiRoomDecisionsForHotelAsync(
                item.Hotel,
                item.SuccessfullyCreatedSupplierRoomIds ?? new HashSet<int>(),
                cancellationToken);
            var drafts = processor.BuildAiDraftsForHotel(item.Hotel, result);
            var successIds = await processor.PersistCreatedRoomsWithResultsAsync(
                drafts,
                item.OutputMode ?? string.Empty,
                cancellationToken);
            await processor.RecordAiCreateResultsAsync(result, drafts, successIds, cancellationToken);
            _logger.LogInformation(
                "AI room branch completed. MTHotelCode={MTHotelCode}, CandidateCount={CandidateCount}, SuitableCount={SuitableCount}, CreatedCount={CreatedCount}",
                item.Hotel.MTHotelCode,
                result.Candidates.Count,
                drafts.Count,
                successIds.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.RecordError("ai_room_stage");
            _logger.LogError(ex, "AI room branch failed. MTHotelCode={MTHotelCode}", item.Hotel?.MTHotelCode);
        }
        finally
        {
            _metrics.DecrementPendingHotels();
        }
    }

    static async Task DrainAiRoomTasksAsync(
        HashSet<Task> inFlight,
        CancellationToken cancellationToken,
        bool waitForAll)
    {
        if (inFlight.Count == 0)
        {
            return;
        }

        if (waitForAll)
        {
            await Task.WhenAll(inFlight).WaitAsync(cancellationToken);
            inFlight.Clear();
            return;
        }

        await Task.WhenAny(inFlight).WaitAsync(cancellationToken);
        foreach (var task in inFlight.Where(task => task.IsCompleted).ToArray())
        {
            inFlight.Remove(task);
            await task.WaitAsync(cancellationToken);
        }
    }
}

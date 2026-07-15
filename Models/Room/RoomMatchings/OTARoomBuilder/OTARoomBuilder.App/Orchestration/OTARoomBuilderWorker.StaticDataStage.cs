using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
    async Task RunStaticDataAsync(CancellationToken cancellationToken)
    {
        var inFlight = new HashSet<Task>();

        while (await _staticDataChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_staticDataChannel.Reader.TryRead(out var item))
            {
                _metrics.RecordQueueDequeued("static_data");
                if (item.Completion is not null)
                {
                    await DrainStaticDataTasksAsync(inFlight, cancellationToken, waitForAll: true);
                    await _roomCreateChannel.Writer.WriteAsync(
                        RoomCreateQueueItem.ForCompletion(item.Completion),
                        cancellationToken);
                    _metrics.RecordQueueEnqueued("room_create");
                    continue;
                }

                var task = RunStaticDataStageSafelyAsync(item, cancellationToken);
                inFlight.Add(task);

                var maxConcurrency = ClampQueueCapacity(_options.CurrentValue.StaticDataTaskConcurrency);
                if (inFlight.Count >= maxConcurrency)
                {
                    await DrainStaticDataTasksAsync(inFlight, cancellationToken, waitForAll: false);
                }
            }

            await DrainStaticDataTasksAsync(inFlight, cancellationToken, waitForAll: false);
        }

        await DrainStaticDataTasksAsync(inFlight, cancellationToken, waitForAll: true);
    }

    async Task RunStaticDataStageSafelyAsync(
        StaticDataQueueItem item,
        CancellationToken cancellationToken)
    {
        var matchResult = item.MatchResult;
        if (matchResult is null)
        {
            return;
        }

        _metrics.RecordStageStarted("static_data");
        var activeTaskCount = Interlocked.Increment(ref _activeStaticDataTasks);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();
            var startedAt = DateTimeOffset.UtcNow;
            _logger.LogInformation(
                "OTARoomBuilder static data stage started. MTHotelCode={MTHotelCode}, CandidateCount={CandidateCount}, ActiveTaskCount={ActiveTaskCount}",
                matchResult.MTHotelCode,
                matchResult.Candidates.Count,
                activeTaskCount);
            var rooms = await processor.EnrichMatchedRoomsForHotelAsync(matchResult, cancellationToken);

            await _roomCreateChannel.Writer.WriteAsync(
                RoomCreateQueueItem.ForHotel(rooms, item.OutputMode ?? string.Empty),
                cancellationToken);
            _metrics.RecordQueueEnqueued("room_create");

            _logger.LogInformation(
                "OTARoomBuilder static data stage completed. MTHotelCode={MTHotelCode}, CandidateCount={CandidateCount}, EnrichedRoomCount={EnrichedRoomCount}, SkippedCount={SkippedCount}, ActiveTaskCount={ActiveTaskCount}, ElapsedMilliseconds={ElapsedMilliseconds}",
                matchResult.MTHotelCode,
                matchResult.Candidates.Count,
                rooms.Count,
                matchResult.Candidates.Count - rooms.Count,
                Volatile.Read(ref _activeStaticDataTasks),
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.RecordError("static_data_stage");
            _logger.LogError(
                ex,
                "OTARoomBuilder static data stage failed. MTHotelCode={MTHotelCode}, CandidateCount={CandidateCount}",
                matchResult.MTHotelCode,
                matchResult.Candidates.Count);

            await _roomCreateChannel.Writer.WriteAsync(
                RoomCreateQueueItem.ForHotel([], item.OutputMode ?? string.Empty),
                cancellationToken);
            _metrics.RecordQueueEnqueued("room_create");
        }
        finally
        {
            Interlocked.Decrement(ref _activeStaticDataTasks);
            _metrics.RecordStageCompleted("static_data");
            _metrics.RecordStageFinished("static_data");
        }
    }

    static async Task DrainStaticDataTasksAsync(
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
        foreach (var task in inFlight.Where(static task => task.IsCompleted).ToArray())
        {
            inFlight.Remove(task);
            await task.WaitAsync(cancellationToken);
        }
    }
}

namespace Tests.Host.HotelMappingSync;

public sealed class HotelMappingSyncRunner(
    IHotelMappingSourceReader sourceReader,
    IHotelMappingRedisStore redisStore,
    IHotelMappingQueueWriter queueWriter)
{
    const int RedisBatchSize = 500;

    public async Task<HotelMappingSyncProgress> RunAsync(
        Action<HotelMappingSyncProgress>? progressChanged,
        CancellationToken cancellationToken)
    {
        var progress = new HotelMappingSyncProgress();
        progress = await DeliverOutboxAsync(progress, progressChanged, cancellationToken);

        var sourceRows = await sourceReader.ReadAsync(cancellationToken);
        var desired = HotelMappingSyncPlanner.BuildDesiredMappings(sourceRows);
        var current = await redisStore.ReadMappingsAsync(cancellationToken);
        var plan = HotelMappingSyncPlanner.CreatePlan(desired, current);

        progress = progress with
        {
            SourceCount = desired.Count,
            RedisCount = current.Count,
            AddedCount = plan.Added.Count,
            UpdatedCount = plan.Updated.Count,
            DeletedCount = plan.Deleted.Count
        };
        progressChanged?.Invoke(progress);

        foreach (var batch in plan.Operations.Chunk(RedisBatchSize))
        {
            await redisStore.ApplyAsync(batch, cancellationToken);
        }

        progress = await DeliverOutboxAsync(progress, progressChanged, cancellationToken);
        return progress;
    }

    async Task<HotelMappingSyncProgress> DeliverOutboxAsync(
        HotelMappingSyncProgress progress,
        Action<HotelMappingSyncProgress>? progressChanged,
        CancellationToken cancellationToken)
    {
        var entries = await redisStore.ReadOutboxAsync(cancellationToken);
        progress = progress with { PendingCount = entries.Count };
        progressChanged?.Invoke(progress);

        foreach (var entry in entries)
        {
            await queueWriter.EnqueueAsync(entry.QueueValue, cancellationToken);
            await redisStore.AcknowledgeOutboxAsync(entry.Id, cancellationToken);
            progress = progress with
            {
                QueuedCount = progress.QueuedCount + 1,
                PendingCount = progress.PendingCount - 1
            };
            progressChanged?.Invoke(progress);
        }

        return progress;
    }
}

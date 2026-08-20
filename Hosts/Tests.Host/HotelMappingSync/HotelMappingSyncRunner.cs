namespace Tests.Host.HotelMappingSync;

using Infrastructure.Redis.HotelMatchingRedis;
public sealed class HotelMappingSyncRunner(
    IHotelMappingSourceReader sourceReader,
    IHotelMappingRedisStore redisStore)
{
    const int RedisBatchSize = 500;

    public async Task<HotelMappingSyncProgress> RunAsync(
        Action<HotelMappingSyncProgress>? progressChanged,
        CancellationToken cancellationToken)
    {
        var progress = new HotelMappingSyncProgress(Phase: "preparingQueue");
        progressChanged?.Invoke(progress);
        var pendingCount = await redisStore.GetPendingQueueLengthAsync(cancellationToken);
        progress = progress with { PendingCount = pendingCount };
        progressChanged?.Invoke(progress);

        progress = progress with { Phase = "loadingSource" };
        progressChanged?.Invoke(progress);
        var sourceRows = await sourceReader.ReadAsync(cancellationToken);
        var desired = HotelMappingSyncPlanner.BuildDesiredMappings(sourceRows);

        progress = progress with
        {
            Phase = "readingRedis",
            SourceCount = desired.Count
        };
        progressChanged?.Invoke(progress);
        var current = await redisStore.ReadMappingsAsync(cancellationToken);

        progress = progress with { Phase = "planning", RedisCount = current.Count };
        progressChanged?.Invoke(progress);
        var plan = HotelMappingSyncPlanner.CreatePlan(desired, current);

        progress = progress with
        {
            Phase = "writingRedis",
            AddedCount = plan.Added.Count,
            UpdatedCount = plan.Updated.Count,
            DeletedCount = plan.Deleted.Count
        };
        progressChanged?.Invoke(progress);

        foreach (var batch in plan.Operations.Chunk(RedisBatchSize))
        {
            await redisStore.ApplyAsync(batch, cancellationToken);
            progress = progress with
            {
                AppliedCount = progress.AppliedCount + batch.Length,
                QueuedCount = progress.QueuedCount + batch.Length,
                PendingCount = progress.PendingCount + batch.Length
            };
            progressChanged?.Invoke(progress);
        }

        progress = progress with { Phase = "completed" };
        progressChanged?.Invoke(progress);
        return progress;
    }
}

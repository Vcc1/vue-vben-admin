using Cronos;
using Infrastructures.RoomMatchingRedis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

public sealed class RoomMatchingStatisticsWorker : BackgroundService
{
    readonly RoomMappingRedis _roomMappingRedis;
    readonly ILogger<RoomMatchingStatisticsWorker> _logger;
    readonly IOptionsMonitor<RoomMatchingStatisticsOptions> _optionsMonitor;
    readonly RoomMatchingStatisticsMetrics _metrics;

    public RoomMatchingStatisticsWorker(
        RoomMappingRedis roomMappingRedis,
        ILogger<RoomMatchingStatisticsWorker> logger,
        IOptionsMonitor<RoomMatchingStatisticsOptions> optionsMonitor,
        RoomMatchingStatisticsMetrics metrics)
    {
        _roomMappingRedis = roomMappingRedis;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runImmediately = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;

            if (!runImmediately || !options.RunImmediatelyOnStartup)
            {
                var delay = GetDelay(options);
                _metrics.SetWaiting();
                _logger.LogInformation("Room matching statistics waiting for next run. Delay={Delay}", delay);
                await Task.Delay(delay, stoppingToken);
            }
            runImmediately = false;

            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            _metrics.StartRun(startedAt);
            _logger.LogInformation(
                "Room matching statistics run started. Version={Version}",
                _metrics.CurrentRunVersion);

            try
            {
                var snapshot = await CollectSnapshotAsync(options, stoppingToken);
                stopwatch.Stop();

                var completedAt = DateTimeOffset.UtcNow;
                _metrics.CompleteRun(snapshot, completedAt, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Room matching statistics run completed. Total={TotalCount}, Expired={ExpiredCount}, DurationMs={DurationMs}",
                    snapshot.TotalCount,
                    snapshot.ExpiredCount,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var failedAt = DateTimeOffset.UtcNow;
                _metrics.FailRun("collect", failedAt, stopwatch.Elapsed);
                _logger.LogError(ex, "Room matching statistics run failed. Retrying in {RetryDelaySeconds}s", options.ErrorRetryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.ErrorRetryDelaySeconds)), stoppingToken);
            }
        }
    }

    async Task<RoomMatchingStatisticsSnapshot> CollectSnapshotAsync(
        RoomMatchingStatisticsOptions options,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var expirationWindowDays = Math.Max(1, options.ExpirationWindowDays);
        long totalCount = 0;
        long expiredCount = 0;
        long unknownExpireTimeCount = 0;
        int processedKeyCount = 0;
        int totalKeyCount = 0;
        var buckets = Enumerable.Range(1, 7).ToDictionary(day => day, _ => 0L);

        void PublishSnapshot(int currentKeyIndex, int totalKeyCount)
        {
            _metrics.UpdateRunningSnapshot(
                new RoomMatchingStatisticsSnapshot(
                    totalCount,
                    expiredCount,
                    unknownExpireTimeCount,
                    new Dictionary<int, long>(buckets),
                    currentKeyIndex,
                    totalKeyCount),
                DateTimeOffset.UtcNow - startedAt,
                currentKeyIndex,
                totalKeyCount);
        }

        _logger.LogInformation("Room matching statistics started streaming redis key statistics.");
        await _roomMappingRedis.VisitKeyStatisticsAsync(async (stat, currentIndex, totalKeys) =>
        {
            processedKeyCount = currentIndex;
            totalKeyCount = totalKeys;

            _logger.LogInformation(
                "Room matching statistics processing redis key. Current={CurrentIndex}, Total={TotalKeys}, Key={RedisKey}",
                currentIndex,
                totalKeys,
                stat.Key);

            totalCount += stat.ItemCount;

            if (!stat.TimeToLive.HasValue)
            {
                unknownExpireTimeCount += stat.ItemCount;
                PublishSnapshot(currentIndex, totalKeys);
                _logger.LogInformation(
                    "Room matching statistics progress. Current={CurrentIndex}, Total={TotalKeys}, Key={RedisKey}, ItemCount={ItemCount}, TotalCount={TotalCount}, ExpiredCount={ExpiredCount}, UnknownExpireTimeCount={UnknownExpireTimeCount}",
                    currentIndex,
                    totalKeys,
                    stat.Key,
                    stat.ItemCount,
                    totalCount,
                    expiredCount,
                    unknownExpireTimeCount);
                await Task.CompletedTask;
                return;
            }

            var ttl = stat.TimeToLive.Value;
            if (ttl <= TimeSpan.Zero)
            {
                expiredCount += stat.ItemCount;
                PublishSnapshot(currentIndex, totalKeys);
                _logger.LogInformation(
                    "Room matching statistics progress. Current={CurrentIndex}, Total={TotalKeys}, Key={RedisKey}, ItemCount={ItemCount}, TotalCount={TotalCount}, ExpiredCount={ExpiredCount}, UnknownExpireTimeCount={UnknownExpireTimeCount}",
                    currentIndex,
                    totalKeys,
                    stat.Key,
                    stat.ItemCount,
                    totalCount,
                    expiredCount,
                    unknownExpireTimeCount);
                await Task.CompletedTask;
                return;
            }

            var remainingDays = ttl.TotalDays;
            if (remainingDays <= expirationWindowDays)
            {
                var bucket = (int)Math.Ceiling(remainingDays);
                bucket = Math.Clamp(bucket, 1, 7);
                buckets[bucket] += stat.ItemCount;
            }

            PublishSnapshot(currentIndex, totalKeys);

            _logger.LogInformation(
                "Room matching statistics progress. Current={CurrentIndex}, Total={TotalKeys}, Key={RedisKey}, ItemCount={ItemCount}, TotalCount={TotalCount}, ExpiredCount={ExpiredCount}, UnknownExpireTimeCount={UnknownExpireTimeCount}",
                currentIndex,
                totalKeys,
                stat.Key,
                stat.ItemCount,
                totalCount,
                expiredCount,
                unknownExpireTimeCount);
            await Task.CompletedTask;
        }, cancellationToken: cancellationToken);

        PublishSnapshot(processedKeyCount, totalKeyCount);

        _logger.LogInformation(
            "Room matching statistics finished streaming redis key statistics. TotalCount={TotalCount}, ExpiredCount={ExpiredCount}, UnknownExpireTimeCount={UnknownExpireTimeCount}",
            totalCount,
            expiredCount,
            unknownExpireTimeCount);

        return new RoomMatchingStatisticsSnapshot(
            totalCount,
            expiredCount,
            unknownExpireTimeCount,
            buckets,
            processedKeyCount,
            totalKeyCount);
    }

    TimeSpan GetDelay(RoomMatchingStatisticsOptions options)
    {
        var cron = CronExpression.Parse(options.Cron, CronFormat.IncludeSeconds);
        var now = DateTimeOffset.Now;
        var next = cron.GetNextOccurrence(now, TimeZoneInfo.Local);

        if (!next.HasValue)
        {
            return TimeSpan.FromMinutes(10);
        }

        var delay = next.Value - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
    }
}

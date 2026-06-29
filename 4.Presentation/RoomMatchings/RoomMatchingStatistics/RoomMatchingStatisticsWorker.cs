using Cronos;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Diagnostics;

public sealed class RoomMatchingStatisticsWorker : BackgroundService
{
    readonly IServiceProvider _serviceProvider;
    readonly ILogger<RoomMatchingStatisticsWorker> _logger;
    readonly IOptionsMonitor<RoomMatchingStatisticsOptions> _optionsMonitor;
    readonly RoomMatchingStatisticsMetrics _metrics;

    public RoomMatchingStatisticsWorker(
        IServiceProvider serviceProvider,
        ILogger<RoomMatchingStatisticsWorker> logger,
        IOptionsMonitor<RoomMatchingStatisticsOptions> optionsMonitor,
        RoomMatchingStatisticsMetrics metrics)
    {
        _serviceProvider = serviceProvider;
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
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT_BIG(*) AS TotalCount,
                COALESCE(SUM(CASE WHEN CreateDate IS NULL THEN 1 ELSE 0 END), 0) AS UnknownCreateDateCount,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) <= @now
                    THEN 1 ELSE 0 END), 0) AS ExpiredCount,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 <= 1
                    THEN 1 ELSE 0 END), 0) AS Day1Count,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 > 1
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 <= 2
                    THEN 1 ELSE 0 END), 0) AS Day2Count,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 > 2
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 <= 3
                    THEN 1 ELSE 0 END), 0) AS Day3Count,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 > 3
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 <= 4
                    THEN 1 ELSE 0 END), 0) AS Day4Count,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 > 4
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 <= 5
                    THEN 1 ELSE 0 END), 0) AS Day5Count,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 > 5
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 <= 6
                    THEN 1 ELSE 0 END), 0) AS Day6Count,
                COALESCE(SUM(CASE
                    WHEN CreateDate IS NOT NULL
                     AND DATEADD(day, @expirationWindowDays, CreateDate) > @now
                     AND DATEDIFF_BIG(second, @now, DATEADD(day, @expirationWindowDays, CreateDate)) / 86400.0 > 6
                    THEN 1 ELSE 0 END), 0) AS Day7Count
            FROM RoomMap WITH (NOLOCK);
            """;

        var nowParameter = command.CreateParameter();
        nowParameter.ParameterName = "@now";
        nowParameter.Value = DateTime.Now;
        command.Parameters.Add(nowParameter);

        var expirationWindowParameter = command.CreateParameter();
        expirationWindowParameter.ParameterName = "@expirationWindowDays";
        expirationWindowParameter.Value = Math.Max(1, options.ExpirationWindowDays);
        command.Parameters.Add(expirationWindowParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new RoomMatchingStatisticsSnapshot(
                0,
                0,
                0,
                Enumerable.Range(1, 7).ToDictionary(day => day, _ => 0L));
        }

        var buckets = new Dictionary<int, long>
        {
            [1] = reader.GetInt64(reader.GetOrdinal("Day1Count")),
            [2] = reader.GetInt64(reader.GetOrdinal("Day2Count")),
            [3] = reader.GetInt64(reader.GetOrdinal("Day3Count")),
            [4] = reader.GetInt64(reader.GetOrdinal("Day4Count")),
            [5] = reader.GetInt64(reader.GetOrdinal("Day5Count")),
            [6] = reader.GetInt64(reader.GetOrdinal("Day6Count")),
            [7] = reader.GetInt64(reader.GetOrdinal("Day7Count"))
        };

        return new RoomMatchingStatisticsSnapshot(
            reader.GetInt64(reader.GetOrdinal("TotalCount")),
            reader.GetInt64(reader.GetOrdinal("ExpiredCount")),
            reader.GetInt64(reader.GetOrdinal("UnknownCreateDateCount")),
            buckets);
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

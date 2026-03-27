using Infrastructure.Sql.SupCacheDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomDbPendingMetricsWorker : BackgroundService
{
    private const int HighPriorityValue = 9;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExpediaChaseRoomMetrics _metrics;
    private readonly ILogger<ExpediaChaseRoomDbPendingMetricsWorker> _logger;

    public ExpediaChaseRoomDbPendingMetricsWorker(
        IServiceScopeFactory scopeFactory,
        ExpediaChaseRoomMetrics metrics,
        ILogger<ExpediaChaseRoomDbPendingMetricsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshCountsSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshCountsSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshCountsSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshCountsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新 Expedia 追房数据库待执行任务指标失败。");
        }
    }

    private async Task RefreshCountsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();

        var counts = await dbContext.ExpediaChaseRoomHotels
            .AsNoTracking()
            .Where(x => x.State == 0)
            .GroupBy(x => x.Priority == HighPriorityValue)
            .Select(group => new
            {
                IsHighPriority = group.Key,
                Count = group.LongCount()
            })
            .ToListAsync(cancellationToken);

        var highPriorityCount = counts.FirstOrDefault(x => x.IsHighPriority)?.Count ?? 0;
        var normalPriorityCount = counts.FirstOrDefault(x => !x.IsHighPriority)?.Count ?? 0;

        _metrics.UpdateDbPendingTaskCounts(highPriorityCount, normalPriorityCount);
    }
}

using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QueryPriceSupRoomBuilder;

public sealed class RoomBuildTaskWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<RoomBuildTaskWorker> _logger;

    public RoomBuildTaskWorker(
        IServiceScopeFactory scopeFactory,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<RoomBuildTaskWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DelayUntilNextRunAsync(stoppingToken);
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _metrics.RecordError("room_build_worker");
                _logger.LogError(ex, "Room build task worker failed.");
                await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.IdleDelaySeconds), stoppingToken);
            }
        }
    }

    async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var targetSuppliers = string.Join(',', options.Suppliers.Where(x => x > 0).Distinct().DefaultIfEmpty(51));
        IReadOnlyList<QueryPriceSupRoomBuildTaskModel> tasks;
        using (var claimScope = _scopeFactory.CreateScope())
        {
            var repository = claimScope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();
            tasks = await repository.ClaimAsync(options.ProcessBatchSize, targetSuppliers, cancellationToken);
        }

        _metrics.RecordTask("claimed", tasks.Count);

        if (tasks.Count == 0)
        {
            using var countScope = _scopeFactory.CreateScope();
            var repository = countScope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();
            _metrics.SetStatusCounts(await repository.CountByStatusAsync(cancellationToken));
            return;
        }

        using var concurrency = new SemaphoreSlim(Math.Max(1, options.DefaultMaxConcurrency));
        var runners = tasks.Select(async task =>
        {
            await concurrency.WaitAsync(cancellationToken);
            using var taskScope = _scopeFactory.CreateScope();
            var repository = taskScope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();
            var processor = taskScope.ServiceProvider.GetRequiredService<QueryPriceSupRoomBuildProcessor>();
            try
            {
                var result = await processor.ProcessAsync(task, cancellationToken);
                if (result.IsCompleted)
                {
                    await repository.MarkCompletedAsync(task.Id, result.QueuedCount, cancellationToken);
                    _metrics.RecordTask("completed");
                }
                else
                {
                    await repository.MarkNoHitAsync(task.Id, cancellationToken);
                    _metrics.RecordTask("retry_waiting_no_hit");
                }
            }
            catch (Exception ex)
            {
                _metrics.RecordError("process_task");
                await repository.MarkRetryOrFailedAsync(task.Id, options.MaxRetryCount, ex.Message, cancellationToken);
                _logger.LogError(ex, "Failed to process room build task. Id={Id}, MTHotelCode={MTHotelCode}", task.Id, task.MTHotelCode);
            }
            finally
            {
                concurrency.Release();
            }
        });

        await Task.WhenAll(runners);
        using var finalCountScope = _scopeFactory.CreateScope();
        var finalRepository = finalCountScope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();
        _metrics.SetStatusCounts(await finalRepository.CountByStatusAsync(cancellationToken));
    }

    async Task DelayUntilNextRunAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.Cron))
        {
            await Task.Delay(TimeSpan.FromSeconds(options.IdleDelaySeconds), cancellationToken);
            return;
        }

        var expression = CronExpression.Parse(options.Cron, CronFormat.IncludeSeconds);
        var next = expression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
        var delay = next is null
            ? TimeSpan.FromSeconds(options.IdleDelaySeconds)
            : next.Value - DateTimeOffset.Now;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }
}

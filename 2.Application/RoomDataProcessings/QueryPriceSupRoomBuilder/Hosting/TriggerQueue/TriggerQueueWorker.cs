using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 触发队列后台任务。
/// 从 Redis 通用触发队列读取 MTHotelCode，并 upsert 到任务表；真正查价由房型构建 pipeline 处理。
/// </summary>
public sealed class TriggerQueueWorker : BackgroundService
{
    readonly ITriggerQueueClient _triggerQueue;
    readonly IServiceScopeFactory _scopeFactory;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<TriggerQueueWorker> _logger;

    public TriggerQueueWorker(
        ITriggerQueueClient triggerQueue,
        IServiceScopeFactory scopeFactory,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<TriggerQueueWorker> logger)
    {
        _triggerQueue = triggerQueue;
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <summary>
    /// 持续消费触发队列；有数据时写任务表，无数据时短暂休眠。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = _optionsMonitor.CurrentValue;
                var values = await _triggerQueue.DequeueRoomBuildHotelsAsync(options.TriggerBatchSize, stoppingToken);
                _metrics.RecordTriggerQueuePop("trigger:room-build:hotel", values.Count);
                if (values.Count > 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();
                    var changed = await repository.UpsertTriggeredAsync(values, stoppingToken);
                    _metrics.RecordTaskUpsert("changed", changed);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.IdleDelaySeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _metrics.RecordError("trigger_queue");
                _logger.LogError(ex, "Trigger queue worker failed.");
                await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.IdleDelaySeconds), stoppingToken);
            }
        }
    }
}

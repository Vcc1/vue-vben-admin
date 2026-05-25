using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace QueryPriceSupRoomBuilder;

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

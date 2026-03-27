using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomAppWorker : BackgroundService
{
    private const string QueueLeaseOwner = "new-taap-queue";

    private readonly ExpediaChaseRoomQueue _queue;
    private readonly ExpediaChaseRoomAppState _state;
    private readonly IExpediaChaseRoomTaskSource _taskSource;
    private readonly IExpediaChaseRoomTaskStore _taskStore;
    private readonly IOptionsMonitor<ExpediaChaseRoomAppOptions> _optionsMonitor;
    private readonly ILogger<ExpediaChaseRoomAppWorker> _logger;

    public ExpediaChaseRoomAppWorker(
        ExpediaChaseRoomQueue queue,
        ExpediaChaseRoomAppState state,
        IExpediaChaseRoomTaskSource taskSource,
        IExpediaChaseRoomTaskStore taskStore,
        IOptionsMonitor<ExpediaChaseRoomAppOptions> optionsMonitor,
        ILogger<ExpediaChaseRoomAppWorker> logger)
    {
        _queue = queue;
        _state = state;
        _taskSource = taskSource;
        _taskStore = taskStore;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _taskStore.UnlockAllProcessingTasksAsync(stoppingToken);
        await ProduceAsync(stoppingToken);
    }

    private async Task ProduceAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            try
            {
                var tasks = await _taskStore.LeasePendingTasksAsync(options.FetchTake, QueueLeaseOwner, cancellationToken);
                if (tasks.Count == 0)
                {
                    var remoteTasks = await _taskSource.FetchPendingTasksAsync(cancellationToken);
                    await _taskStore.SyncTasksAsync(remoteTasks, cancellationToken);
                    tasks = await _taskStore.LeasePendingTasksAsync(options.FetchTake, QueueLeaseOwner, cancellationToken);
                    if (tasks.Count == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(options.IdleDelaySeconds), cancellationToken);
                        continue;
                    }
                }

                foreach (var task in tasks)
                {
                    await _queue.Channel.Writer.WriteAsync(task, cancellationToken);
                    _state.IncreaseQueueRemaining();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "拉取 Expedia 追房任务失败。");
                await Task.Delay(TimeSpan.FromSeconds(options.FetchErrorDelaySeconds), cancellationToken);
            }
        }

        _queue.Channel.Writer.TryComplete();
    }
}

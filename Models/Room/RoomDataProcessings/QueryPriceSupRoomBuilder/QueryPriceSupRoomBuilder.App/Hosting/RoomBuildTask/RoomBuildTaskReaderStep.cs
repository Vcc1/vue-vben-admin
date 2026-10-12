using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 房型构建 pipeline 的任务读取步骤。
/// 从任务表 claim 本轮需要处理的 Pending / RetryWaiting 任务，
/// 并把整组任务一次性写入内存任务队列交给查价步骤。
/// </summary>
public sealed class RoomBuildTaskReaderStep
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<RoomBuildTaskReaderStep> _logger;

    public RoomBuildTaskReaderStep(
        IServiceScopeFactory scopeFactory,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<RoomBuildTaskReaderStep> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <summary>
    /// 读取 Pending / RetryWaiting 且超过重试冷却时间的任务。
    /// 方法结束时关闭任务队列，通知查价步骤不会再有新任务。
    /// </summary>
    public async Task ExecuteAsync(RoomBuildTaskPipeline pipeline, CancellationToken cancellationToken)
    {
        try
        {
            var options = _optionsMonitor.CurrentValue;
            var targetSuppliers = string.Join(',', options.Suppliers.Where(x => x > 0).Distinct().DefaultIfEmpty(51));
            var attemptBefore = DateTime.Now.AddMinutes(-Math.Max(0, options.AttemptCooldownMinutes));
            IReadOnlyList<QueryPriceSupRoomBuildTaskModel> tasks;
            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();
                tasks = await repository.ClaimAsync(targetSuppliers, attemptBefore, cancellationToken);
            }

            _metrics.RecordTask("claimed", tasks.Count);
            _logger.LogInformation(
                "Claimed room build tasks. Count={Count}, AttemptBefore={AttemptBefore}, TargetSuppliers={TargetSuppliers}",
                tasks.Count,
                attemptBefore,
                targetSuppliers);

            if (tasks.Count > 0)
            {
                await pipeline.TaskWriter.WriteAsync(tasks, cancellationToken);
            }

        }
        finally
        {
            pipeline.TaskWriter.TryComplete();
        }
    }
}

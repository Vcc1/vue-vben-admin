using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 房型构建 pipeline 的查价步骤。
/// 消费读取步骤投递的整组酒店任务，调用处理器执行批量查价，
/// 再把每个任务的成功、无命中或异常结果写入持久化队列。
/// </summary>
public sealed class RoomBuildTaskQueryPriceStep
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly RoomBuildTaskMetrics _metrics;
    readonly ILogger<RoomBuildTaskQueryPriceStep> _logger;

    public RoomBuildTaskQueryPriceStep(
        IServiceScopeFactory scopeFactory,
        RoomBuildTaskMetrics metrics,
        ILogger<RoomBuildTaskQueryPriceStep> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// 按任务组执行查价；所有任务组完成后关闭结果队列。
    /// </summary>
    public async Task ExecuteAsync(RoomBuildTaskPipeline pipeline, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Room build query price step started.");
            await foreach (var tasks in pipeline.TaskReader.ReadAllAsync(cancellationToken))
            {
                await RunTaskGroupAsync(tasks, pipeline, cancellationToken);
            }

            _logger.LogInformation("Room build query price step completed.");
        }
        finally
        {
            pipeline.PersistenceWriter.TryComplete();
        }
    }

    async Task RunTaskGroupAsync(
        IReadOnlyList<QueryPriceSupRoomBuildTaskModel> tasks,
        RoomBuildTaskPipeline pipeline,
        CancellationToken cancellationToken)
    {
        var emittedTaskIds = new HashSet<int>();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<QueryPriceSupRoomBuildProcessor>();
            await processor.ProcessBatchAsync(
                tasks,
                async (task, result, token) =>
                {
                    emittedTaskIds.Add(task.Id);
                    await pipeline.PersistenceWriter.WriteAsync(
                        RoomBuildTaskProcessingItem.Success(task, result),
                        token);
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.RecordError("query_price_task_group");
            _logger.LogError(ex, "Failed to query price for room build task group. Count={Count}", tasks.Count);
            foreach (var task in tasks.Where(x => !emittedTaskIds.Contains(x.Id)))
            {
                await pipeline.PersistenceWriter.WriteAsync(RoomBuildTaskProcessingItem.Failure(task, ex), cancellationToken);
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 房型构建 pipeline 的结果持久化步骤。
/// 消费查价步骤输出的单任务处理结果，并把任务状态更新为 Completed、RetryWaiting 或 Failed。
/// </summary>
public sealed class RoomBuildTaskPersistenceStep
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<RoomBuildTaskPersistenceStep> _logger;

    public RoomBuildTaskPersistenceStep(
        IServiceScopeFactory scopeFactory,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<RoomBuildTaskPersistenceStep> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task ExecuteAsync(RoomBuildTaskPipeline pipeline, CancellationToken cancellationToken)
    {
        await foreach (var item in pipeline.PersistenceReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await PersistAsync(item, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _metrics.RecordError("persist_task");
                _logger.LogError(ex, "Failed to persist room build task result. Id={Id}, MTHotelCode={MTHotelCode}", item.Task.Id, item.Task.MTHotelCode);
            }
        }
    }

    async Task PersistAsync(RoomBuildTaskProcessingItem item, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQueryPriceSupRoomBuildTaskRepository>();

        if (item.Error is not null)
        {
            _metrics.RecordError("query_price_task", "failed_task");
            await repository.MarkRetryOrFailedAsync(
                item.Task.Id,
                _optionsMonitor.CurrentValue.MaxRetryCount,
                item.Error.Message,
                cancellationToken);
            await SetStatusCountsAsync(repository, cancellationToken);
            return;
        }

        if (item.Result?.IsCompleted == true)
        {
            await repository.MarkCompletedAsync(item.Task.Id, item.Result.QueuedCount, cancellationToken);
            _metrics.RecordTask("completed");
        }
        else
        {
            _metrics.RecordTask("no_price");
            await repository.MarkNoHitAsync(item.Task.Id, cancellationToken);
            _metrics.RecordTask("retry_waiting_no_hit");
        }

        await SetStatusCountsAsync(repository, cancellationToken);
    }

    async Task SetStatusCountsAsync(
        IQueryPriceSupRoomBuildTaskRepository repository,
        CancellationToken cancellationToken)
    {
        _metrics.SetStatusCounts(await repository.CountByStatusAsync(cancellationToken));
    }
}

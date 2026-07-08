using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 房型构建任务的后台编排器。
/// 每轮创建一组临时 pipeline，依次驱动“读任务 -> 查价 -> 入库”三个步骤，
/// 整轮结束后按 Cron 配置等待下一次执行。
/// </summary>
public sealed class RoomBuildTaskWorker : BackgroundService
{
    readonly RoomBuildTaskReaderStep _readerStep;
    readonly RoomBuildTaskQueryPriceStep _queryPriceStep;
    readonly RoomBuildTaskPersistenceStep _persistenceStep;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<RoomBuildTaskWorker> _logger;

    public RoomBuildTaskWorker(
        RoomBuildTaskReaderStep readerStep,
        RoomBuildTaskQueryPriceStep queryPriceStep,
        RoomBuildTaskPersistenceStep persistenceStep,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<RoomBuildTaskWorker> logger)
    {
        _readerStep = readerStep;
        _queryPriceStep = queryPriceStep;
        _persistenceStep = persistenceStep;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Worker 主循环：执行一轮完整 pipeline，然后按 Cron 等待下一轮。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Room build task worker started.");
        _metrics.RecordTask("worker_started");
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
                await DelayUntilNextRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _metrics.RecordError("room_build_pipeline");
                _logger.LogError(ex, "Room build task pipeline failed.");
                await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.IdleDelaySeconds), stoppingToken);
            }
        }
    }

    /// <summary>
    /// 执行一轮房型构建 pipeline。
    /// 入库步骤和查价步骤先启动等待队列；读取步骤写完任务后会关闭任务队列，
    /// 后续步骤读到完成标记后自然退出。
    /// </summary>
    async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        _metrics.SetOuterProgress(0, 0);
        _metrics.SetInnerProgress(0, 0);
        _logger.LogInformation("Room build task pipeline round started. StartedAt={StartedAt}", startedAt);
        _metrics.RecordTask("round_started");
        var pipeline = new RoomBuildTaskPipeline();
        var persistenceTask = _persistenceStep.ExecuteAsync(pipeline, cancellationToken);
        var queryPriceTask = _queryPriceStep.ExecuteAsync(pipeline, cancellationToken);
        await _readerStep.ExecuteAsync(pipeline, cancellationToken);
        await queryPriceTask;
        await persistenceTask;
        _logger.LogInformation(
            "Room build task pipeline round completed. StartedAt={StartedAt}, DurationMs={DurationMs}",
            startedAt,
            (long)(DateTimeOffset.Now - startedAt).TotalMilliseconds);
        _metrics.SetInnerProgress(0, 0);
        _metrics.SetOuterProgress(0, 0);
        _metrics.RecordTask("round_completed");
    }

    /// <summary>
    /// 等待到配置 Cron 的下一次触发时间；无 Cron 时按 IdleDelaySeconds 短暂休眠。
    /// </summary>
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
            _logger.LogInformation("Room build task worker waiting for next round. NextRunAt={NextRunAt}, DelaySeconds={DelaySeconds}", next, (long)delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

public class Worker : BackgroundService
{
    private static readonly (string StageName, string QueueName, string DisplayName)[] MetricStages =
    {
        ("load_mt_hotel_codes", "hotel_codes", "加载酒店编码 / Load hotel codes"),
        ("resolve_sup_hotel_mappings", "resolved_sup_hotels", "解析供应商酒店映射 / Resolve supplier hotel mappings"),
        ("fetch_ean_static_hotels", "ean_hotels", "获取EAN静态酒店 / Fetch EAN static hotels"),
        ("merge_sup_static_room_info", "merged_ean_hotels", "合并供应商静态房型 / Merge supplier static room info"),
        ("load_hotel_room_data", "hotel_room_data", "加载酒店房型数据 / Load hotel room data"),
        ("parse_hotel_rooms", "parsed_hotels", "解析酒店房型 / Parse hotel rooms"),
        ("save_parsed_hotels", "parsed_hotels", "保存解析结果 / Save parsed hotels")
    };

    readonly ILogger<Worker> _logger;
    readonly IServiceScopeFactory _scopeFactory;
    readonly ILoggerFactory _loggerFactory;
    readonly IOptionsMonitor<OtaRoomInfoCompletionOptions> _optionsMonitor;
    readonly OtaRoomInfoCompletionMetrics _metrics;
    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<OtaRoomInfoCompletionOptions> optionsMonitor,
        OtaRoomInfoCompletionMetrics metrics)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _optionsMonitor = optionsMonitor;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("母房型信息补全服务已启动");

        using var metricsLogCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var metricsLogTask = LogMetricsPeriodicallyAsync(metricsLogCts.Token);

        try
        {
            _logger.LogInformation("服务启动后立即执行一次，当前时间: {Now}", DateTime.Now);
            await RunPipelineAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayToNextDailyRunTime(DateTimeOffset.Now);
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("等待到下一个下午3点开始执行，剩余: {Delay}", delay);
                    await Task.Delay(delay, stoppingToken);
                }

                await RunPipelineAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "母房型信息补全服务执行异常");
        }
        finally
        {
            metricsLogCts.Cancel();
            try
            {
                await metricsLogTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _logger.LogInformation("母房型信息补全服务已停止");
    }

    private async Task LogMetricsPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            LogCurrentMetrics();
        }
    }

    private void LogCurrentMetrics()
    {
        var queueDepths = _metrics.GetQueueDepthSnapshot();
        var completedTasks = _metrics.GetCompletedTaskSnapshot();
        var errorCounts = _metrics.GetErrorSnapshot();
        var runningTasks = _metrics.GetRunningTaskSnapshot();
        var stageLines = string.Join(Environment.NewLine, MetricStages.Select(stage => FormatStageMetrics(stage, queueDepths, runningTasks, completedTasks, errorCounts)));

        //_logger.LogInformation(
        //    "10秒指标快照 / 10-second metrics snapshot{NewLine}{StageLines}",
        //    Environment.NewLine,
        //    stageLines);
    }

    private static string FormatStageMetrics(
        (string StageName, string QueueName, string DisplayName) stage,
        IReadOnlyDictionary<string, long> queueDepths,
        IReadOnlyDictionary<string, long> runningTasks,
        IReadOnlyDictionary<string, long> completedTasks,
        IReadOnlyDictionary<string, long> errorCounts)
    {
        var queueDepth = queueDepths.TryGetValue(stage.QueueName, out var queueValue) ? queueValue : 0;
        var runningTaskCount = runningTasks.TryGetValue(stage.StageName, out var runningValue) ? runningValue : 0;
        var completedTaskCount = completedTasks.TryGetValue(stage.StageName, out var completedValue) ? completedValue : 0;
        var errorCount = errorCounts.TryGetValue(stage.StageName, out var errorValue) ? errorValue : 0;
        return $"{stage.DisplayName}: {queueDepth}-{runningTaskCount}-{completedTaskCount}-{errorCount} (队列剩余数量-进行数-已完成数-错误数 / Queue remaining-In progress-Completed-Errors)";
    }

    private async Task RunPipelineAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始执行每日调度任务，当前时间: {Now}", DateTime.Now);
        var pipeline = new OtaRoomInfoCompletionPipeline(_scopeFactory, _loggerFactory, _optionsMonitor.CurrentValue, _metrics);
        await pipeline.RunAsync(cancellationToken);
        _logger.LogInformation("当前轮次执行完成，当前时间: {Now}", DateTime.Now);
        _metrics.Reset();
    }

    private static TimeSpan GetDelayToNextDailyRunTime(DateTimeOffset now)
    {
        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, 15, 0, 0, now.Offset);
        if (now >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }
}

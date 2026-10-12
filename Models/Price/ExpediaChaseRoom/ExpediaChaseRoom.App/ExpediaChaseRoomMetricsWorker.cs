using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomMetricsWorker : BackgroundService
{
    private readonly ExpediaChaseRoomAppState _state;
    private readonly IOptionsMonitor<ExpediaChaseRoomAppOptions> _optionsMonitor;
    private readonly ILogger<ExpediaChaseRoomMetricsWorker> _logger;

    public ExpediaChaseRoomMetricsWorker(
        ExpediaChaseRoomAppState state,
        IOptionsMonitor<ExpediaChaseRoomAppOptions> optionsMonitor,
        ILogger<ExpediaChaseRoomMetricsWorker> logger)
    {
        _state = state;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("队列剩余数量：{Count}", _state.QueueRemaining);
            _logger.LogInformation("正在处理数量：{Count}", _state.InProgress);
            _logger.LogInformation("已完成数量：{Count}", _state.Completed);

            await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.MetricsLogIntervalSeconds), stoppingToken);
        }
    }
}

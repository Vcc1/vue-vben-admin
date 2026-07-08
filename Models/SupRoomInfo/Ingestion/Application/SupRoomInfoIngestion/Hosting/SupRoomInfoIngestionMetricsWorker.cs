namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoIngestionMetricsWorker : BackgroundService
{
    readonly SupRoomInfoIngestionState _state;
    readonly IOptionsMonitor<SupRoomInfoIngestionOptions> _optionsMonitor;
    readonly ILogger<SupRoomInfoIngestionMetricsWorker> _logger;

    public SupRoomInfoIngestionMetricsWorker(
        SupRoomInfoIngestionState state,
        IOptionsMonitor<SupRoomInfoIngestionOptions> optionsMonitor,
        ILogger<SupRoomInfoIngestionMetricsWorker> logger)
    {
        _state = state;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            _logger.LogInformation("queue remaining: {QueueRemaining}", _state.QueueRemaining);
            _logger.LogInformation("in progress: {InProgress}", _state.InProgress);
            _logger.LogInformation("completed: {Completed}", _state.Completed);
            _logger.LogInformation("list pending: {ListPending}", _state.ListPending);
            _logger.LogInformation("set pending: {SetPending}", _state.SetPending);
            _logger.LogInformation("dedup cache count: {DedupCacheCount}", _state.DedupCacheCount);
            _logger.LogInformation("dedup cache hit: {DedupCacheHit}", _state.DedupCacheHit);
            _logger.LogInformation("dedup cache miss: {DedupCacheMiss}", _state.DedupCacheMiss);
            _logger.LogInformation("dedup cache hit rate: {DedupCacheHitRate}", _state.DedupCacheHitRate);
            await Task.Delay(TimeSpan.FromSeconds(options.MetricsLogIntervalSeconds), stoppingToken);
        }
    }
}

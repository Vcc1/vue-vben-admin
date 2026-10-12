namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoProduceStep
{
    readonly SupRoomInfoSourceCoordinatorStep _sourceCoordinatorStep;
    readonly SupRoomInfoIngestionQueue _queue;
    readonly SupRoomInfoIngestionState _state;
    readonly SupRoomInfoIngestionMetrics _metrics;
    readonly IOptionsMonitor<SupRoomInfoIngestionOptions> _optionsMonitor;
    readonly ILogger<SupRoomInfoProduceStep> _logger;

    public SupRoomInfoProduceStep(
        SupRoomInfoSourceCoordinatorStep sourceCoordinatorStep,
        SupRoomInfoIngestionQueue queue,
        SupRoomInfoIngestionState state,
        SupRoomInfoIngestionMetrics metrics,
        IOptionsMonitor<SupRoomInfoIngestionOptions> optionsMonitor,
        ILogger<SupRoomInfoProduceStep> logger)
    {
        _sourceCoordinatorStep = sourceCoordinatorStep;
        _queue = queue;
        _state = state;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var items = await _sourceCoordinatorStep.ReadNextAsync(cancellationToken);
                if (items.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.IdleDelaySeconds), cancellationToken);
                    continue;
                }

                foreach (var item in items)
                {
                    await _queue.Channel.Writer.WriteAsync(item, cancellationToken);
                }

                _state.IncreaseQueueRemaining(items.Count);
                _metrics.RecordPipelineStep("queue_enqueued", items.Count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _metrics.RecordError("produce_loop");
                _logger.LogError(ex, "生产 SupRoomInfo 队列失败，10 秒后重试。");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}

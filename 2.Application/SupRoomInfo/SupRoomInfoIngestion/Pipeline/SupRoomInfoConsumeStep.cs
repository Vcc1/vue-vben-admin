namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoConsumeStep
{
    readonly SupRoomInfoIngestionQueue _queue;
    readonly SupRoomInfoIngestionState _state;
    readonly SupRoomInfoBatchWriteStep _batchWriteStep;
    readonly SupRoomInfoIngestionMetrics _metrics;
    readonly IOptionsMonitor<SupRoomInfoIngestionOptions> _optionsMonitor;
    readonly ILogger<SupRoomInfoConsumeStep> _logger;

    public SupRoomInfoConsumeStep(
        SupRoomInfoIngestionQueue queue,
        SupRoomInfoIngestionState state,
        SupRoomInfoBatchWriteStep batchWriteStep,
        SupRoomInfoIngestionMetrics metrics,
        IOptionsMonitor<SupRoomInfoIngestionOptions> optionsMonitor,
        ILogger<SupRoomInfoConsumeStep> logger)
    {
        _queue = queue;
        _state = state;
        _batchWriteStep = batchWriteStep;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var batch = new List<TempSupRoomInfo2>(_optionsMonitor.CurrentValue.WriteBatchSize);
        await foreach (var item in _queue.Channel.Reader.ReadAllAsync(cancellationToken))
        {
            _state.DecreaseQueueRemaining(1);
            batch.Add(item);
            if (batch.Count < _optionsMonitor.CurrentValue.WriteBatchSize)
            {
                continue;
            }

            await FlushAsync(batch, cancellationToken);
        }

        if (batch.Count > 0)
        {
            await FlushAsync(batch, cancellationToken);
        }
    }

    async Task FlushAsync(List<TempSupRoomInfo2> batch, CancellationToken cancellationToken)
    {
        var processingCount = batch.Count;
        try
        {
            _state.IncreaseInProgress(processingCount);
            var insertedCount = await _batchWriteStep.WriteAsync(batch, cancellationToken);
            _state.IncreaseCompleted(insertedCount);
            _metrics.RecordPipelineStep("queue_consumed", processingCount);
        }
        catch (Exception ex)
        {
            _metrics.RecordError("consume_flush");
            _logger.LogError(ex, "消费 SupRoomInfo 批次失败。count={Count}", processingCount);
        }
        finally
        {
            _state.DecreaseInProgress(processingCount);
            batch.Clear();
        }
    }
}

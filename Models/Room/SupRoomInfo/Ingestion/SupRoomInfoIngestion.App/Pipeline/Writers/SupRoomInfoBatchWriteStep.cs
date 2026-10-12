using SupRoomInfoIngestion.Contracts;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoBatchWriteStep
{
    readonly ISupRoomInfoBatchWriter _batchWriter;
    readonly SupRoomInfoNormalizeStep _normalizeStep;
    readonly SupRoomInfoIngestionMetrics _metrics;
    readonly ILogger<SupRoomInfoBatchWriteStep> _logger;

    public SupRoomInfoBatchWriteStep(
        ISupRoomInfoBatchWriter batchWriter,
        SupRoomInfoNormalizeStep normalizeStep,
        SupRoomInfoIngestionMetrics metrics,
        ILogger<SupRoomInfoBatchWriteStep> logger)
    {
        _batchWriter = batchWriter;
        _normalizeStep = normalizeStep;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<int> WriteAsync(IReadOnlyList<TempSupRoomInfo2> batch, CancellationToken cancellationToken)
    {
        using var scope = _metrics.MeasureDuration("write_batch");
        var normalized = _normalizeStep.Normalize(batch);
        if (normalized.Count == 0)
        {
            return 0;
        }

        try
        {
            return await _batchWriter.WriteAsync(normalized, cancellationToken);
        }
        catch (Exception ex)
        {
            _metrics.RecordError("write_batch");
            _logger.LogError(ex, "写入 SupRoomInfo 批次失败。count={Count}", normalized.Count);
            throw;
        }
    }
}

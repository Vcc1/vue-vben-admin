using SupRoomInfoIngestion.Contracts;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoSetSourceStep : ISupRoomInfoSourceStep
{
    readonly ISupRoomInfoSetReader _setReader;
    readonly IOptionsMonitor<SupRoomInfoIngestionOptions> _optionsMonitor;
    readonly SupRoomInfoIngestionMetrics _metrics;

    public SupRoomInfoSetSourceStep(
        ISupRoomInfoSetReader setReader,
        IOptionsMonitor<SupRoomInfoIngestionOptions> optionsMonitor,
        SupRoomInfoIngestionMetrics metrics)
    {
        _setReader = setReader;
        _optionsMonitor = optionsMonitor;
        _metrics = metrics;
    }

    public string SourceName => "set";

    public async Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(CancellationToken cancellationToken)
    {
        using var scope = _metrics.MeasureDuration("set_read");
        var options = _optionsMonitor.CurrentValue;
        var items = await _setReader.ReadAsync(options.SetKeyPrefix, options.SetFetchBatchSize, cancellationToken);
        _metrics.RecordSourceFetched(SourceName, items.Count);
        return items;
    }

    public Task<long> GetPendingCountAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        return _setReader.GetPendingCountAsync(options.SetKeyPrefix, cancellationToken);
    }
}

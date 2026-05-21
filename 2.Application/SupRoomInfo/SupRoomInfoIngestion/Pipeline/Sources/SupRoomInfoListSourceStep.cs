using SupRoomInfoIngestion.Contracts;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoListSourceStep : ISupRoomInfoSourceStep
{
    readonly ISupRoomInfoListReader _listReader;
    readonly IOptionsMonitor<SupRoomInfoIngestionOptions> _optionsMonitor;
    readonly SupRoomInfoIngestionMetrics _metrics;

    public SupRoomInfoListSourceStep(
        ISupRoomInfoListReader listReader,
        IOptionsMonitor<SupRoomInfoIngestionOptions> optionsMonitor,
        SupRoomInfoIngestionMetrics metrics)
    {
        _listReader = listReader;
        _optionsMonitor = optionsMonitor;
        _metrics = metrics;
    }

    public string SourceName => "list";

    public async Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(CancellationToken cancellationToken)
    {
        using var scope = _metrics.MeasureDuration("list_read");
        var options = _optionsMonitor.CurrentValue;
        var items = await _listReader.ReadAsync(options.FetchBatchSize, cancellationToken);
        _metrics.RecordSourceFetched(SourceName, items.Count);
        return items;
    }

    public Task<long> GetPendingCountAsync(CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        return _listReader.GetPendingCountAsync(options.ListKey, cancellationToken);
    }
}

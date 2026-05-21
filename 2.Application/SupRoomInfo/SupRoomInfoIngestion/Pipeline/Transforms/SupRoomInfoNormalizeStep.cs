namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoNormalizeStep
{
    readonly SupRoomInfoIngestionMetrics _metrics;

    public SupRoomInfoNormalizeStep(SupRoomInfoIngestionMetrics metrics)
    {
        _metrics = metrics;
    }

    public List<TempSupRoomInfo2> Normalize(IReadOnlyList<TempSupRoomInfo2> items)
    {
        using var scope = _metrics.MeasureDuration("normalize");
        var normalized = items
            .Select(static item =>
            {
                item.SRoomCode ??= string.Empty;
                item.BedInfoKey ??= string.Empty;
                item.BedInfo ??= "[]";
                item.SRoomNameCN ??= string.Empty;
                item.SRoomNameEN ??= string.Empty;
                item.SHotelCode ??= string.Empty;
                return item;
            })
            .Where(static item => item.SRoomNameCN.Length < 200 && item.SRoomNameEN.Length < 200 && item.SRoomCode.Length < 200)
            .GroupBy(item => new
            {
                item.SupId,
                item.SHotelCode,
                item.SRoomNameCN,
                item.SRoomNameEN,
                item.BedInfoKey,
                item.BedInfo,
                item.SmokeType,
                item.AcreageFrom,
                item.AcreageTo,
            })
            .Select(static group => group.Last())
            .ToList();

        _metrics.RecordPipelineStep("normalize_in", items.Count);
        _metrics.RecordPipelineStep("normalize_out", normalized.Count);
        return normalized;
    }
}

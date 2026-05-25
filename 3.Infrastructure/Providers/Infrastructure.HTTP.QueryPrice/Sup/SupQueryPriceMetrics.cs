using System.Diagnostics.Metrics;
using QueryPriceSupRoomBuilder;
using VBase.AppMetrics;

namespace Infrastructure.HTTP.QueryPrice.Sup;

public sealed class SupQueryPriceMetrics
{
    readonly Counter<long> _requestTotal;
    readonly Counter<long> _resultTotal;
    readonly Histogram<double> _durationMs;

    public SupQueryPriceMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(QueryPriceSupRoomBuilderMetricScopes.QueryPrice).Meter;
        _requestTotal = meter.CreateCounter<long>("query_price_request_total", unit: "requests");
        _resultTotal = meter.CreateCounter<long>("query_price_result_total", unit: "requests");
        _durationMs = meter.CreateHistogram<double>("query_price_duration_ms", unit: "ms");
    }

    public void RecordRequest(string supId, int hotelCount)
    {
        _requestTotal.Add(1, Tag("sup_id", supId), Tag("outcome", "started"), Tag("hotel_count", hotelCount.ToString()));
    }

    public void RecordResult(string supId, int hotelCount, string outcome)
    {
        _resultTotal.Add(1, Tag("sup_id", supId), Tag("outcome", outcome), Tag("hotel_count", hotelCount.ToString()));
    }

    public void RecordDuration(string supId, int hotelCount, string outcome, TimeSpan duration)
    {
        _durationMs.Record(
            Math.Max(0, duration.TotalMilliseconds),
            Tag("sup_id", supId),
            Tag("outcome", outcome),
            Tag("hotel_count", hotelCount.ToString()));
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }
}

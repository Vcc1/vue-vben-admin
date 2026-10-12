using System.Diagnostics.Metrics;

namespace HotelPriceAvailability.App;

public sealed class HotelPriceAvailabilityMetrics : IDisposable
{
    public const string MeterName = "HotelPriceAvailability";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _requests;
    private readonly Counter<long> _pricedTasks;
    private readonly Histogram<double> _jobDuration;

    public HotelPriceAvailabilityMetrics()
    {
        _requests = _meter.CreateCounter<long>("hotel_price_availability.requests");
        _pricedTasks = _meter.CreateCounter<long>("hotel_price_availability.priced_tasks");
        _jobDuration = _meter.CreateHistogram<double>("hotel_price_availability.job.duration", "s");
    }

    public void RecordRequest(int supId, bool successful) =>
        _requests.Add(1, new KeyValuePair<string, object?>("sup_id", supId),
            new KeyValuePair<string, object?>("outcome", successful ? "success" : "failure"));

    public void RecordPricedTasks(int count) => _pricedTasks.Add(count);

    public void RecordJobDuration(TimeSpan elapsed) => _jobDuration.Record(elapsed.TotalSeconds);

    public void Dispose() => _meter.Dispose();
}

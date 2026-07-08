using System.Diagnostics.Metrics;
using VBase.AppMetrics;

namespace Infrastructure.Redis.TriggerQueue;

public sealed class TriggerQueueMetrics
{
    const string Scope = "Infrastructure.Redis.TriggerQueue";

    readonly Counter<long> _queueOperationTotal;

    public TriggerQueueMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(Scope).Meter;
        _queueOperationTotal = meter.CreateCounter<long>("trigger_queue_operation_total", unit: "items");
    }

    public void Record(string queue, string operation, string outcome, long count)
    {
        if (count <= 0)
        {
            return;
        }

        _queueOperationTotal.Add(
            count,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("stage", operation),
            new KeyValuePair<string, object?>("outcome", outcome));
    }
}

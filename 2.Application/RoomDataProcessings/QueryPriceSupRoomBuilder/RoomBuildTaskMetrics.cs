using System.Diagnostics.Metrics;
using OTADomain.RoomDataProcessings;
using VBase.AppMetrics;

namespace QueryPriceSupRoomBuilder;

public sealed class RoomBuildTaskMetrics
{
    readonly Counter<long> _triggerQueuePopTotal;
    readonly Counter<long> _triggerTaskUpsertTotal;
    readonly Counter<long> _roomBuildTaskTotal;
    readonly ObservableGauge<long> _roomBuildTaskStatusCount;
    readonly Counter<long> _priceHitTotal;
    readonly Counter<long> _priceFilteredTotal;
    readonly Counter<long> _supRoomRedisPushTotal;
    readonly Counter<long> _roomBuildErrorTotal;
    readonly Dictionary<QueryPriceSupRoomBuildTaskStatus, long> _statusCounts = [];
    readonly object _statusLock = new();

    public RoomBuildTaskMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(QueryPriceSupRoomBuilderMetricScopes.App).Meter;
        _triggerQueuePopTotal = meter.CreateCounter<long>("trigger_queue_pop_total", unit: "items");
        _triggerTaskUpsertTotal = meter.CreateCounter<long>("trigger_task_upsert_total", unit: "tasks");
        _roomBuildTaskTotal = meter.CreateCounter<long>("room_build_task_total", unit: "tasks");
        _priceHitTotal = meter.CreateCounter<long>("price_hit_total", unit: "rooms");
        _priceFilteredTotal = meter.CreateCounter<long>("price_filtered_total", unit: "rooms");
        _supRoomRedisPushTotal = meter.CreateCounter<long>("sup_room_redis_push_total", unit: "rooms");
        _roomBuildErrorTotal = meter.CreateCounter<long>("room_build_error_total", unit: "errors");
        _roomBuildTaskStatusCount = meter.CreateObservableGauge(
            "room_build_task_status_count",
            ObserveStatusCounts,
            unit: "tasks");
    }

    public void RecordTriggerQueuePop(string queue, int count)
    {
        if (count > 0)
        {
            _triggerQueuePopTotal.Add(count, Tag("queue", queue));
        }
    }

    public void RecordTaskUpsert(string outcome, int count)
    {
        if (count > 0)
        {
            _triggerTaskUpsertTotal.Add(count, Tag("outcome", outcome));
        }
    }

    public void RecordTask(string status, int count = 1)
    {
        _roomBuildTaskTotal.Add(count, Tag("status", status));
    }

    public void RecordPriceHit(int supId, int count)
    {
        if (count > 0)
        {
            _priceHitTotal.Add(count, Tag("sup_id", supId.ToString()));
        }
    }

    public void RecordFiltered(int supId, string reason, int count = 1)
    {
        _priceFilteredTotal.Add(count, Tag("sup_id", supId.ToString()), Tag("reason", reason));
    }

    public void RecordRedisPush(string outcome, int count)
    {
        if (count > 0)
        {
            _supRoomRedisPushTotal.Add(count, Tag("outcome", outcome));
        }
    }

    public void RecordError(string stage, string outcome = "error")
    {
        _roomBuildErrorTotal.Add(1, Tag("stage", stage), Tag("outcome", outcome));
    }

    public void SetStatusCounts(IReadOnlyDictionary<QueryPriceSupRoomBuildTaskStatus, long> counts)
    {
        lock (_statusLock)
        {
            _statusCounts.Clear();
            foreach (var item in counts)
            {
                _statusCounts[item.Key] = item.Value;
            }
        }
    }

    IEnumerable<Measurement<long>> ObserveStatusCounts()
    {
        lock (_statusLock)
        {
            return _statusCounts
                .Select(x => new Measurement<long>(x.Value, Tag("status", x.Key.ToString())))
                .ToArray();
        }
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }
}

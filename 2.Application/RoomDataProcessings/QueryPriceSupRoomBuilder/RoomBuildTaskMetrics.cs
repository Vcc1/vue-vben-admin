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
    readonly ObservableGauge<long> _roomBuildProgressGauge;
    readonly ObservableGauge<long> _roomBuildLoopProgressGauge;
    readonly Counter<long> _priceHitTotal;
    readonly Counter<long> _priceFilteredTotal;
    readonly Counter<long> _supRoomRedisPushTotal;
    readonly Counter<long> _roomBuildErrorTotal;
    readonly Counter<long> _queryPriceNoPriceTotal;
    readonly Counter<long> _queryPriceErrorCodeTotal;
    readonly QueryProgressSnapshot _progressSnapshot = new();
    readonly LoopProgressSnapshot _loopProgressSnapshot = new();
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
        _roomBuildProgressGauge = meter.CreateObservableGauge(
            "room_build_progress",
            ObserveProgress,
            unit: "items");
        _roomBuildLoopProgressGauge = meter.CreateObservableGauge(
            "room_build_loop_progress",
            ObserveLoopProgress,
            unit: "items");
        _queryPriceNoPriceTotal = meter.CreateCounter<long>("query_price_no_price_total", unit: "hotels");
        _queryPriceErrorCodeTotal = meter.CreateCounter<long>("query_price_error_code_total", unit: "requests");
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

    public void RecordNoPrice(int supId, int count)
    {
        if (count > 0)
        {
            _queryPriceNoPriceTotal.Add(count, Tag("sup_id", supId.ToString()));
        }
    }

    public void RecordQueryPriceErrorCode(int supId, string? code)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? "unknown" : code;
        _queryPriceErrorCodeTotal.Add(1, Tag("sup_id", supId.ToString()), Tag("code", normalizedCode));
    }

    public void SetOuterProgress(long totalTasks, long completedTasks)
    {
        lock (_progressSnapshot.SyncRoot)
        {
            _progressSnapshot.OuterTotalTasks = Math.Max(0, totalTasks);
            _progressSnapshot.OuterCompletedTasks = Math.Max(0, Math.Min(completedTasks, totalTasks));
            _progressSnapshot.OuterRemainingTasks = Math.Max(0, totalTasks - completedTasks);
        }
    }

    public void SetInnerProgress(long totalHotels, long completedHotels)
    {
        lock (_progressSnapshot.SyncRoot)
        {
            _progressSnapshot.InnerTotalHotels = Math.Max(0, totalHotels);
            _progressSnapshot.InnerCompletedHotels = Math.Max(0, Math.Min(completedHotels, totalHotels));
            _progressSnapshot.InnerRemainingHotels = Math.Max(0, totalHotels - completedHotels);
        }
    }

    public void SetDimensionLoopProgress(long totalDimensions, long currentDimension, long runningDimensions)
    {
        lock (_loopProgressSnapshot.SyncRoot)
        {
            _loopProgressSnapshot.DimensionTotal = Math.Max(0, totalDimensions);
            _loopProgressSnapshot.DimensionCurrent = Clamp(currentDimension, totalDimensions);
            _loopProgressSnapshot.DimensionRunning = Math.Max(0, runningDimensions);
        }
    }

    public void SetHotelLoopProgress(long totalHotels, long currentHotels, long runningBatches)
    {
        lock (_loopProgressSnapshot.SyncRoot)
        {
            _loopProgressSnapshot.HotelTotal = Math.Max(0, totalHotels);
            _loopProgressSnapshot.HotelCurrent = Clamp(currentHotels, totalHotels);
            _loopProgressSnapshot.HotelRunning = Math.Max(0, runningBatches);
        }
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

    IEnumerable<Measurement<long>> ObserveProgress()
    {
        lock (_progressSnapshot.SyncRoot)
        {
            return
            [
                new Measurement<long>(_progressSnapshot.OuterTotalTasks, Tag("scope", "outer"), Tag("state", "total")),
                new Measurement<long>(_progressSnapshot.OuterCompletedTasks, Tag("scope", "outer"), Tag("state", "completed")),
                new Measurement<long>(_progressSnapshot.OuterRemainingTasks, Tag("scope", "outer"), Tag("state", "remaining")),
                new Measurement<long>(_progressSnapshot.InnerTotalHotels, Tag("scope", "inner"), Tag("state", "total")),
                new Measurement<long>(_progressSnapshot.InnerCompletedHotels, Tag("scope", "inner"), Tag("state", "completed")),
                new Measurement<long>(_progressSnapshot.InnerRemainingHotels, Tag("scope", "inner"), Tag("state", "remaining"))
            ];
        }
    }

    IEnumerable<Measurement<long>> ObserveLoopProgress()
    {
        lock (_loopProgressSnapshot.SyncRoot)
        {
            return
            [
                new Measurement<long>(_loopProgressSnapshot.DimensionTotal, Tag("loop", "dimension"), Tag("state", "total")),
                new Measurement<long>(_loopProgressSnapshot.DimensionCurrent, Tag("loop", "dimension"), Tag("state", "current")),
                new Measurement<long>(_loopProgressSnapshot.DimensionRunning, Tag("loop", "dimension"), Tag("state", "running")),
                new Measurement<long>(_loopProgressSnapshot.HotelTotal, Tag("loop", "hotel"), Tag("state", "total")),
                new Measurement<long>(_loopProgressSnapshot.HotelCurrent, Tag("loop", "hotel"), Tag("state", "current")),
                new Measurement<long>(_loopProgressSnapshot.HotelRunning, Tag("loop", "hotel"), Tag("state", "running"))
            ];
        }
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }

    static long Clamp(long value, long max)
    {
        return Math.Max(0, Math.Min(value, Math.Max(0, max)));
    }

    sealed class QueryProgressSnapshot
    {
        public object SyncRoot { get; } = new();
        public long OuterTotalTasks { get; set; }
        public long OuterCompletedTasks { get; set; }
        public long OuterRemainingTasks { get; set; }
        public long InnerTotalHotels { get; set; }
        public long InnerCompletedHotels { get; set; }
        public long InnerRemainingHotels { get; set; }
    }

    sealed class LoopProgressSnapshot
    {
        public object SyncRoot { get; } = new();
        public long DimensionTotal { get; set; }
        public long DimensionCurrent { get; set; }
        public long DimensionRunning { get; set; }
        public long HotelTotal { get; set; }
        public long HotelCurrent { get; set; }
        public long HotelRunning { get; set; }
    }
}

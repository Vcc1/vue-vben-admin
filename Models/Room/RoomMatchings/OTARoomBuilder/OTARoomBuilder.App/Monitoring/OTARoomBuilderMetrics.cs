using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using VBase.AppMetrics;

namespace OTARoomBuilder;

public sealed class OTARoomBuilderMetrics
{
    public const string ScopeName = "OTARoomBuilder";

    static readonly string[] OutputModes = ["create", "post", "bulk", "database"];
    static readonly string[] SkipReasons =
    [
        "already_matched",
        "max_adt_missing",
        "no_new_rooms",
        "plugin_missing",
        "supplier_rooms_empty"
    ];
    static readonly string[] ErrorStages =
    [
        "FullSync",
        "Incremental",
        "create_stage",
        "incremental_scan",
        "match_stage",
        "static_data_stage",
        "post_room"
    ];
    static readonly string[] StaticDataRoomMatchOutcomes =
    [
        "matched",
        "lookup_failed",
        "max_occupancy_missing",
        "room_code_not_found",
        "unknown"
    ];
    static readonly string[] StageNames = ["load", "match", "static_data", "create"];
    static readonly string[] QueueNames = ["job", "hotel_build", "static_data", "room_create"];
    static readonly IReadOnlyDictionary<string, int> StageIndexes =
        StageNames.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
    static readonly IReadOnlyDictionary<string, int> QueueIndexes =
        QueueNames.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);

    readonly Counter<long> _runCounter;
    readonly Counter<long> _hotelCounter;
    readonly Counter<long> _createdCounter;
    readonly Counter<long> _skippedCounter;
    readonly Counter<long> _errorCounter;
    readonly Counter<long> _staticDataRoomMatchCounter;
    readonly Histogram<double> _durationMs;
    readonly ObservableGauge<long> _pendingHotelsGauge;
    readonly ObservableGauge<long> _createdHotelsGauge;
    readonly ObservableGauge<long> _lastRunCreatedGauge;
    readonly Counter<long> _stageCompletedCounter;
    readonly long[] _stageRunning = new long[StageNames.Length];
    readonly long[] _queueDepth = new long[QueueNames.Length];

    long _pendingHotels;
    long _createdHotels;
    long _lastRunCreated;

    public OTARoomBuilderMetrics(
        IMetricsFactory metricsFactory,
        IOptions<OTARoomBuilderOptions> options)
    {
        var meter = metricsFactory.Create(ScopeName).Meter;
        _runCounter = meter.CreateCounter<long>("ota_room_builder_run_total", unit: "runs");
        _hotelCounter = meter.CreateCounter<long>("ota_room_builder_hotel_total", unit: "hotels");
        _createdCounter = meter.CreateCounter<long>("ota_room_builder_created_total", unit: "rooms");
        _skippedCounter = meter.CreateCounter<long>("ota_room_builder_skipped_total", unit: "rooms");
        _errorCounter = meter.CreateCounter<long>("ota_room_builder_error_total", unit: "errors");
        _staticDataRoomMatchCounter = meter.CreateCounter<long>("ota_room_builder_static_data_room_match_total", unit: "rooms");
        _durationMs = meter.CreateHistogram<double>("ota_room_builder_duration_ms", unit: "ms");
        _pendingHotelsGauge = meter.CreateObservableGauge("ota_room_builder_pending_hotels", () => Interlocked.Read(ref _pendingHotels), unit: "hotels");
        _createdHotelsGauge = meter.CreateObservableGauge("ota_room_builder_created_hotels", () => Interlocked.Read(ref _createdHotels), unit: "rooms");
        _lastRunCreatedGauge = meter.CreateObservableGauge("ota_room_builder_last_run_created", () => Interlocked.Read(ref _lastRunCreated), unit: "rooms");
        _stageCompletedCounter = meter.CreateCounter<long>("ota_room_builder_stage_completed_total", unit: "hotels");

        for (var index = 0; index < StageNames.Length; index++)
        {
            var stage = StageNames[index];
            var stageIndex = index;
            meter.CreateObservableGauge(
                $"ota_room_builder_stage_{stage}_running_hotels",
                () => Interlocked.Read(ref _stageRunning[stageIndex]),
                unit: "hotels");
        }

        for (var index = 0; index < QueueNames.Length; index++)
        {
            var queue = QueueNames[index];
            var queueIndex = index;
            meter.CreateObservableGauge(
                $"ota_room_builder_queue_{queue}_depth",
                () => Interlocked.Read(ref _queueDepth[queueIndex]),
                unit: "items");
        }

        InitializeDefaults(options.Value.GetEnabledSupIds());
    }

    public IDisposable TrackRun()
    {
        _runCounter.Add(1);
        var startedAt = Stopwatch.GetTimestamp();
        return new DelegatingDisposable(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            _durationMs.Record(Math.Max(0, elapsed.TotalMilliseconds));
        });
    }

    public void SetPendingHotels(int count) => Interlocked.Exchange(ref _pendingHotels, count);

    public void DecrementPendingHotels()
    {
        var remaining = Interlocked.Decrement(ref _pendingHotels);
        if (remaining < 0)
        {
            Interlocked.Exchange(ref _pendingHotels, 0);
        }
    }

    public void RecordHotelBatch(int count)
    {
        if (count > 0)
        {
            _hotelCounter.Add(count);
        }
    }

    public void RecordCreated(int count, string outputMode)
    {
        if (count <= 0)
        {
            return;
        }

        _createdCounter.Add(count, new KeyValuePair<string, object?>("output_mode", outputMode));
        Interlocked.Add(ref _createdHotels, count);
        Interlocked.Exchange(ref _lastRunCreated, count);
    }

    public void RecordSkipped(int count, string reason)
    {
        if (count <= 0)
        {
            return;
        }

        _skippedCounter.Add(count, new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordError(string stage)
    {
        _errorCounter.Add(1, new KeyValuePair<string, object?>("stage", stage));
        Telemetry.Errors.Add(1, new KeyValuePair<string, object?>("stage", stage));
    }

    public void RecordStaticDataRoomMatch(int supId, string outcome)
    {
        _staticDataRoomMatchCounter.Add(
            1,
            new KeyValuePair<string, object?>("sup_id", supId.ToString()),
            new KeyValuePair<string, object?>("outcome", string.IsNullOrWhiteSpace(outcome) ? "unknown" : outcome));
    }

    public void RecordStageStarted(string stage, int count = 1)
    {
        if (count <= 0 || !StageIndexes.TryGetValue(stage, out var index))
        {
            return;
        }

        Interlocked.Add(ref _stageRunning[index], count);
    }

    public void RecordStageCompleted(string stage, int count = 1)
    {
        if (count <= 0 || !StageIndexes.ContainsKey(stage))
        {
            return;
        }

        _stageCompletedCounter.Add(count, Tag("stage", stage));
    }

    public void RecordStageFinished(string stage, int count = 1)
    {
        if (count <= 0 || !StageIndexes.TryGetValue(stage, out var index))
        {
            return;
        }

        var remaining = Interlocked.Add(ref _stageRunning[index], -count);
        if (remaining < 0)
        {
            Interlocked.Exchange(ref _stageRunning[index], 0);
        }
    }

    public void RecordQueueEnqueued(string queue, int count = 1)
    {
        if (count <= 0 || !QueueIndexes.TryGetValue(queue, out var index))
        {
            return;
        }

        Interlocked.Add(ref _queueDepth[index], count);
    }

    public void RecordQueueDequeued(string queue, int count = 1)
    {
        if (count <= 0 || !QueueIndexes.TryGetValue(queue, out var index))
        {
            return;
        }

        var remaining = Interlocked.Add(ref _queueDepth[index], -count);
        if (remaining < 0)
        {
            Interlocked.Exchange(ref _queueDepth[index], 0);
        }
    }

    public void ResetRun()
    {
        Interlocked.Exchange(ref _pendingHotels, 0);
        Interlocked.Exchange(ref _createdHotels, 0);
        Interlocked.Exchange(ref _lastRunCreated, 0);
    }

    void InitializeDefaults(IReadOnlyList<int> enabledSupIds)
    {
        _runCounter.Add(0);
        _hotelCounter.Add(0);
        _durationMs.Record(0);

        foreach (var outputMode in OutputModes)
        {
            _createdCounter.Add(0, Tag("output_mode", outputMode));
        }

        foreach (var reason in SkipReasons)
        {
            _skippedCounter.Add(0, Tag("reason", reason));
        }

        foreach (var stage in ErrorStages)
        {
            var stageTag = Tag("stage", stage);
            _errorCounter.Add(0, stageTag);
            Telemetry.Errors.Add(0, stageTag);
        }

        foreach (var supId in enabledSupIds)
        {
            foreach (var outcome in StaticDataRoomMatchOutcomes)
            {
                _staticDataRoomMatchCounter.Add(
                    0,
                    Tag("sup_id", supId.ToString()),
                    Tag("outcome", outcome));
            }
        }
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }

    private sealed class DelegatingDisposable(Action dispose) : IDisposable
    {
        int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            dispose();
        }
    }
}

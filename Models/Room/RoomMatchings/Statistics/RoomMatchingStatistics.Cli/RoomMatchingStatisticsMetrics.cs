using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using VBase.AppMetrics;

public sealed class RoomMatchingStatisticsMetrics
{
    public const string ScopeName = "RoomMatchingStatistics";

    readonly Counter<long> _runCounter;
    readonly Counter<long> _errorCounter;
    readonly Histogram<double> _durationMs;

    readonly object _snapshotLock = new();

    long _currentRunVersion;
    long _workerState;
    long _currentRunStartedAtUnix;
    long _lastRunCompletedAtUnix;
    long _lastSuccessfulCompletedAtUnix;
    long _lastRunSucceeded;
    long _lastRunCompleted;
    long _lastErrorAtUnix;
    long _lastDurationMs;
    long _currentKeyIndex;
    long _currentTotalKeyCount;
    long _totalCount;
    long _expiredCount;
    long _unknownCreateDateCount;

    IReadOnlyDictionary<int, long> _remainingDayBuckets = new ReadOnlyDictionary<int, long>(
        Enumerable.Range(1, 7).ToDictionary(day => day, _ => 0L));

    public RoomMatchingStatisticsMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(ScopeName).Meter;

        meter.CreateObservableGauge("room_matching_statistics_run_version", () => Interlocked.Read(ref _currentRunVersion));
        meter.CreateObservableGauge("room_matching_statistics_worker_state", () => Interlocked.Read(ref _workerState));
        meter.CreateObservableGauge("room_matching_statistics_current_run_started_at", () => Interlocked.Read(ref _currentRunStartedAtUnix));
        meter.CreateObservableGauge("room_matching_statistics_last_run_completed_at", () => Interlocked.Read(ref _lastRunCompletedAtUnix));
        meter.CreateObservableGauge("room_matching_statistics_last_success_completed_at", () => Interlocked.Read(ref _lastSuccessfulCompletedAtUnix));
        meter.CreateObservableGauge("room_matching_statistics_last_run_succeeded", () => Interlocked.Read(ref _lastRunSucceeded));
        meter.CreateObservableGauge("room_matching_statistics_last_run_completed", () => Interlocked.Read(ref _lastRunCompleted));
        meter.CreateObservableGauge("room_matching_statistics_last_error_at", () => Interlocked.Read(ref _lastErrorAtUnix));
        meter.CreateObservableGauge("room_matching_statistics_last_duration_ms", () => Interlocked.Read(ref _lastDurationMs), unit: "ms");
        meter.CreateObservableGauge("room_matching_statistics_current_key_index", () => Interlocked.Read(ref _currentKeyIndex), unit: "keys");
        meter.CreateObservableGauge("room_matching_statistics_current_total_key_count", () => Interlocked.Read(ref _currentTotalKeyCount), unit: "keys");
        meter.CreateObservableGauge("room_matching_statistics_total", () => Interlocked.Read(ref _totalCount), unit: "rows");
        meter.CreateObservableGauge("room_matching_statistics_expired_total", () => Interlocked.Read(ref _expiredCount), unit: "rows");
        meter.CreateObservableGauge("room_matching_statistics_unknown_create_date_total", () => Interlocked.Read(ref _unknownCreateDateCount), unit: "rows");
        meter.CreateObservableGauge("room_matching_statistics_expiring_days", ObserveRemainingDayBuckets, unit: "rows");

        _runCounter = meter.CreateCounter<long>("room_matching_statistics_run_total", unit: "runs");
        _errorCounter = meter.CreateCounter<long>("room_matching_statistics_error_total", unit: "errors");
        _durationMs = meter.CreateHistogram<double>("room_matching_statistics_duration_ms", unit: "ms");

        SetWaiting();
    }

    public long CurrentRunVersion => Interlocked.Read(ref _currentRunVersion);

    public void StartRun(DateTimeOffset startedAt)
    {
        Interlocked.Increment(ref _currentRunVersion);
        Interlocked.Exchange(ref _workerState, 1);
        Interlocked.Exchange(ref _currentRunStartedAtUnix, startedAt.ToUnixTimeSeconds());
        Interlocked.Exchange(ref _lastRunCompleted, 0);
        Interlocked.Exchange(ref _lastRunSucceeded, 0);
        Interlocked.Exchange(ref _currentKeyIndex, 0);
        Interlocked.Exchange(ref _currentTotalKeyCount, 0);
        _runCounter.Add(1, Tag("outcome", "started"));
    }

    public void UpdateRunningSnapshot(RoomMatchingStatisticsSnapshot snapshot, TimeSpan elapsed, int currentKeyIndex, int totalKeyCount)
    {
        lock (_snapshotLock)
        {
            _remainingDayBuckets = new ReadOnlyDictionary<int, long>(
                snapshot.RemainingDayBuckets.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        Interlocked.Exchange(ref _totalCount, snapshot.TotalCount);
        Interlocked.Exchange(ref _expiredCount, snapshot.ExpiredCount);
        Interlocked.Exchange(ref _unknownCreateDateCount, snapshot.UnknownCreateDateCount);
        Interlocked.Exchange(ref _lastDurationMs, Math.Max(0, (long)elapsed.TotalMilliseconds));
        Interlocked.Exchange(ref _currentKeyIndex, currentKeyIndex);
        Interlocked.Exchange(ref _currentTotalKeyCount, totalKeyCount);
    }

    public void CompleteRun(RoomMatchingStatisticsSnapshot snapshot, DateTimeOffset completedAt, TimeSpan duration)
    {
        UpdateRunningSnapshot(snapshot, duration, (int)snapshot.ProcessedKeyCount, (int)snapshot.TotalKeyCount);
        Interlocked.Exchange(ref _workerState, 2);
        Interlocked.Exchange(ref _lastRunCompletedAtUnix, completedAt.ToUnixTimeSeconds());
        Interlocked.Exchange(ref _lastSuccessfulCompletedAtUnix, completedAt.ToUnixTimeSeconds());
        Interlocked.Exchange(ref _lastRunSucceeded, 1);
        Interlocked.Exchange(ref _lastRunCompleted, 1);

        _runCounter.Add(1, Tag("outcome", "completed"));
        _durationMs.Record(Math.Max(0, duration.TotalMilliseconds));
    }

    public void FailRun(string stage, DateTimeOffset failedAt, TimeSpan? duration)
    {
        Interlocked.Exchange(ref _workerState, 3);
        Interlocked.Exchange(ref _lastRunCompletedAtUnix, failedAt.ToUnixTimeSeconds());
        Interlocked.Exchange(ref _lastRunSucceeded, 0);
        Interlocked.Exchange(ref _lastRunCompleted, 0);
        Interlocked.Exchange(ref _lastErrorAtUnix, failedAt.ToUnixTimeSeconds());

        if (duration.HasValue)
        {
            Interlocked.Exchange(ref _lastDurationMs, Math.Max(0, (long)duration.Value.TotalMilliseconds));
        }

        _errorCounter.Add(1, Tag("stage", stage));
        _runCounter.Add(1, Tag("outcome", "error"));
        Telemetry.Errors.Add(1, Tag("stage", stage));
    }

    public void SetWaiting()
    {
        Interlocked.Exchange(ref _workerState, 4);
        Interlocked.Exchange(ref _currentKeyIndex, 0);
        Interlocked.Exchange(ref _currentTotalKeyCount, 0);
    }

    IEnumerable<Measurement<long>> ObserveRemainingDayBuckets()
    {
        KeyValuePair<int, long>[] snapshot;
        lock (_snapshotLock)
        {
            snapshot = _remainingDayBuckets.OrderByDescending(pair => pair.Key).ToArray();
        }

        foreach (var pair in snapshot)
        {
            yield return new Measurement<long>(pair.Value, Tag("days", pair.Key.ToString()));
        }
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }
}

public sealed record RoomMatchingStatisticsSnapshot(
    long TotalCount,
    long ExpiredCount,
    long UnknownCreateDateCount,
    IReadOnlyDictionary<int, long> RemainingDayBuckets,
    long ProcessedKeyCount,
    long TotalKeyCount);

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using VBase.AppMetrics;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomMetrics
{
    private readonly ObservableGauge<long> _dbPendingTaskGauge;
    private readonly Counter<long> _taskResultCounter;
    private readonly Histogram<double> _operationDurationHistogram;
    private readonly Counter<long> _sessionEventCounter;
    private readonly ObservableGauge<long> _accountWorkerGauge;
    private readonly ObservableGauge<long> _accountInProgressGauge;
    private readonly ConcurrentDictionary<string, long> _accountWorkerCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _accountInProgressCounts = new(StringComparer.OrdinalIgnoreCase);
    private long _highPriorityPendingCount;
    private long _normalPriorityPendingCount;

    public ExpediaChaseRoomMetrics(DefaultMetrics metrics)
    {
        var meter = metrics.Meter;
        _dbPendingTaskGauge = meter.CreateObservableGauge(
            "expedia_chase_room_db_pending_task_count",
            ObserveDbPendingTaskCounts,
            unit: "tasks");
        _taskResultCounter = meter.CreateCounter<long>(
            "expedia_chase_room_task_total",
            unit: "tasks");
        _operationDurationHistogram = meter.CreateHistogram<double>(
            "expedia_chase_room_operation_duration",
            unit: "ms");
        _sessionEventCounter = meter.CreateCounter<long>(
            "expedia_taap_session_total",
            unit: "events");
        _accountWorkerGauge = meter.CreateObservableGauge(
            "expedia_taap_account_worker_count",
            ObserveAccountWorkerCounts,
            unit: "workers");
        _accountInProgressGauge = meter.CreateObservableGauge(
            "expedia_taap_account_in_progress_task_count",
            ObserveAccountInProgressCounts,
            unit: "tasks");
    }

    public void UpdateDbPendingTaskCounts(long highPriorityCount, long normalPriorityCount)
    {
        Volatile.Write(ref _highPriorityPendingCount, Math.Max(0, highPriorityCount));
        Volatile.Write(ref _normalPriorityPendingCount, Math.Max(0, normalPriorityCount));
    }

    public void RecordTaskResult(string? accountName, string result)
    {
        _taskResultCounter.Add(
            1,
            new KeyValuePair<string, object?>("account_name", NormalizeAccountName(accountName)),
            new KeyValuePair<string, object?>("result", result));
    }

    public void RecordOperationDuration(string? accountName, string operation, TimeSpan duration)
    {
        _operationDurationHistogram.Record(
            Math.Max(0, duration.TotalMilliseconds),
            new KeyValuePair<string, object?>("account_name", NormalizeAccountName(accountName)),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordSessionEvent(string? accountName, string eventName)
    {
        _sessionEventCounter.Add(
            1,
            new KeyValuePair<string, object?>("account_name", NormalizeAccountName(accountName)),
            new KeyValuePair<string, object?>("event", eventName));
    }

    public void SetAccountWorkerCount(string? accountName, int workerCount)
    {
        SetSnapshotValue(_accountWorkerCounts, accountName, workerCount);
    }

    public void IncreaseAccountInProgress(string? accountName)
    {
        AddSnapshotValue(_accountInProgressCounts, accountName, 1);
    }

    public void DecreaseAccountInProgress(string? accountName)
    {
        AddSnapshotValue(_accountInProgressCounts, accountName, -1);
    }

    private IEnumerable<Measurement<long>> ObserveDbPendingTaskCounts()
    {
        yield return new Measurement<long>(
            Volatile.Read(ref _highPriorityPendingCount),
            new KeyValuePair<string, object?>("priority", "high"));

        yield return new Measurement<long>(
            Volatile.Read(ref _normalPriorityPendingCount),
            new KeyValuePair<string, object?>("priority", "normal"));
    }

    private IEnumerable<Measurement<long>> ObserveAccountWorkerCounts()
    {
        foreach (var item in _accountWorkerCounts)
        {
            yield return new Measurement<long>(
                item.Value,
                new KeyValuePair<string, object?>("account_name", item.Key));
        }
    }

    private IEnumerable<Measurement<long>> ObserveAccountInProgressCounts()
    {
        foreach (var item in _accountInProgressCounts)
        {
            yield return new Measurement<long>(
                item.Value,
                new KeyValuePair<string, object?>("account_name", item.Key));
        }
    }

    private static string NormalizeAccountName(string? accountName)
    {
        return string.IsNullOrWhiteSpace(accountName)
            ? "unknown"
            : accountName.Trim();
    }

    private static void SetSnapshotValue(
        ConcurrentDictionary<string, long> snapshot,
        string? accountName,
        long value)
    {
        var normalizedAccountName = NormalizeAccountName(accountName);
        if (value <= 0)
        {
            snapshot.TryRemove(normalizedAccountName, out _);
            return;
        }

        snapshot[normalizedAccountName] = value;
    }

    private static void AddSnapshotValue(
        ConcurrentDictionary<string, long> snapshot,
        string? accountName,
        long delta)
    {
        var normalizedAccountName = NormalizeAccountName(accountName);
        snapshot.AddOrUpdate(
            normalizedAccountName,
            _ => Math.Max(0, delta),
            (_, current) => Math.Max(0, current + delta));

        if (snapshot.TryGetValue(normalizedAccountName, out var currentValue) &&
            currentValue == 0)
        {
            snapshot.TryRemove(normalizedAccountName, out _);
        }
    }
}

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using VBase.AppMetrics;

public sealed class OtaRoomInfoCompletionMetrics
{
    private const string CompletedTaskMetricName = "pipeline_completed_task_total";
    private const string ErrorMetricName = "pipeline_error_total";

    private readonly ConcurrentDictionary<string, long> _queueDepths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _runningTaskCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _completedTaskCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _errorCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Counter<long> _completedTaskCounter;
    private readonly Counter<long> _errorCounter;
    private readonly ObservableGauge<long> _queueDepthGauge;
    private readonly ObservableGauge<long> _runningTaskGauge;

    public OtaRoomInfoCompletionMetrics(DefaultMetrics metrics)
    {
        var meter = metrics.Meter;
        _completedTaskCounter = meter.CreateCounter<long>(CompletedTaskMetricName, unit: "tasks");
        _errorCounter = meter.CreateCounter<long>(ErrorMetricName, unit: "errors");
        _queueDepthGauge = meter.CreateObservableGauge("ota_room_info_completion_queue_depth", ObserveQueueDepth, unit: "items");
        _runningTaskGauge = meter.CreateObservableGauge("ota_room_info_completion_running_task_count", ObserveRunningTasks, unit: "tasks");
    }

    public void IncreaseQueueDepth(string queueName, int delta = 1)
    {
        if (delta <= 0)
        {
            return;
        }

        _queueDepths.AddOrUpdate(queueName, delta, (_, current) => current + delta);
    }

    public void DecreaseQueueDepth(string queueName, int delta = 1)
    {
        if (delta <= 0)
        {
            return;
        }

        _queueDepths.AddOrUpdate(queueName, 0, (_, current) => Math.Max(0, current - delta));
    }

    public void IncreaseRunningTasks(string stageName, int delta = 1)
    {
        if (delta <= 0)
        {
            return;
        }

        _runningTaskCounts.AddOrUpdate(stageName, delta, (_, current) => current + delta);
    }

    public void DecreaseRunningTasks(string stageName, int delta = 1)
    {
        if (delta <= 0)
        {
            return;
        }

        _runningTaskCounts.AddOrUpdate(stageName, 0, (_, current) => Math.Max(0, current - delta));
    }

    public void RecordCompletedTasks(string stageName, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        _completedTaskCounts.AddOrUpdate(stageName, count, (_, current) => current + count);
        _completedTaskCounter.Add(count, new KeyValuePair<string, object?>("stage", stageName));
    }

    public void RecordErrors(string stageName, int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        _errorCounts.AddOrUpdate(stageName, count, (_, current) => current + count);
        _errorCounter.Add(count, new KeyValuePair<string, object?>("stage", stageName));
    }

    public IReadOnlyDictionary<string, long> GetQueueDepthSnapshot()
    {
        return GetSnapshot(_queueDepths);
    }

    public IReadOnlyDictionary<string, long> GetRunningTaskSnapshot()
    {
        return GetSnapshot(_runningTaskCounts);
    }

    public IReadOnlyDictionary<string, long> GetCompletedTaskSnapshot()
    {
        return GetSnapshot(_completedTaskCounts);
    }

    public IReadOnlyDictionary<string, long> GetErrorSnapshot()
    {
        return GetSnapshot(_errorCounts);
    }

    public void Reset()
    {
        _queueDepths.Clear();
        _runningTaskCounts.Clear();
        _completedTaskCounts.Clear();
        _errorCounts.Clear();
    }

    private static IReadOnlyDictionary<string, long> GetSnapshot(ConcurrentDictionary<string, long> source)
    {
        return source
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<Measurement<long>> ObserveQueueDepth()
    {
        foreach (var item in _queueDepths)
        {
            yield return new Measurement<long>(item.Value, new KeyValuePair<string, object?>("queue", item.Key));
        }
    }

    private IEnumerable<Measurement<long>> ObserveRunningTasks()
    {
        foreach (var item in _runningTaskCounts)
        {
            yield return new Measurement<long>(item.Value, new KeyValuePair<string, object?>("stage", item.Key));
        }
    }
}

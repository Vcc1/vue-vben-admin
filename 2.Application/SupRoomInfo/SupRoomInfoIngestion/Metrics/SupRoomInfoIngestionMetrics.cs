using System.Collections.Generic;
using System.Threading;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoIngestionMetrics
{
    readonly Counter<long> _sourceFetchCounter;
    readonly Counter<long> _sourceEmptyCounter;
    readonly Counter<long> _pipelineStepCounter;
    readonly Counter<long> _errorCounter;
    readonly Histogram<double> _durationHistogram;
    readonly Histogram<long> _batchSizeHistogram;
    readonly ObservableGauge<long> _queueGauge;
    readonly ObservableGauge<long> _listPendingGauge;
    readonly ObservableGauge<long> _setPendingGauge;
    readonly ObservableGauge<long> _inProgressGauge;
    readonly ObservableGauge<long> _completedGauge;
    readonly SupRoomInfoIngestionState _state;

    public SupRoomInfoIngestionMetrics(DefaultMetrics metrics, SupRoomInfoIngestionState state)
    {
        _state = state;
        var meter = metrics.Meter;
        _sourceFetchCounter = meter.CreateCounter<long>("sup_room_info_ingestion_source_fetch_total", unit: "rooms");
        _sourceEmptyCounter = meter.CreateCounter<long>("sup_room_info_ingestion_source_empty_total", unit: "times");
        _pipelineStepCounter = meter.CreateCounter<long>("sup_room_info_ingestion_pipeline_step_total", unit: "rooms");
        _errorCounter = meter.CreateCounter<long>("sup_room_info_ingestion_error_total", unit: "errors");
        _durationHistogram = meter.CreateHistogram<double>("sup_room_info_ingestion_duration", unit: "ms");
        _batchSizeHistogram = meter.CreateHistogram<long>("sup_room_info_ingestion_batch_size", unit: "rooms");
        _queueGauge = meter.CreateObservableGauge("sup_room_info_ingestion_queue_remaining", () => _state.QueueRemaining, unit: "rooms");
        _listPendingGauge = meter.CreateObservableGauge("sup_room_info_ingestion_list_pending", () => _state.ListPending, unit: "rooms");
        _setPendingGauge = meter.CreateObservableGauge("sup_room_info_ingestion_set_pending", () => _state.SetPending, unit: "rooms");
        _inProgressGauge = meter.CreateObservableGauge("sup_room_info_ingestion_in_progress", () => _state.InProgress, unit: "rooms");
        _completedGauge = meter.CreateObservableGauge("sup_room_info_ingestion_completed", () => _state.Completed, unit: "rooms");
    }

    public IDisposable MeasureDuration(string step)
    {
        var startedAt = DateTime.UtcNow;
        return new DelegatingDisposable(() =>
        {
            var duration = DateTime.UtcNow - startedAt;
            _durationHistogram.Record(
                Math.Max(0, duration.TotalMilliseconds),
                new KeyValuePair<string, object?>("step", step));
        });
    }

    public void RecordSourceFetched(string source, int count)
    {
        if (count <= 0)
        {
            _sourceEmptyCounter.Add(1, new KeyValuePair<string, object?>("source", source));
            return;
        }

        _sourceFetchCounter.Add(count, new KeyValuePair<string, object?>("source", source));
        _batchSizeHistogram.Record(count, new KeyValuePair<string, object?>("source", source));
    }

    public void RecordPipelineStep(string step, int count)
    {
        if (count <= 0)
        {
            return;
        }

        _pipelineStepCounter.Add(count, new KeyValuePair<string, object?>("step", step));
    }

    public void RecordError(string step)
    {
        _errorCounter.Add(1, new KeyValuePair<string, object?>("step", step));
        Telemetry.Errors.Add(1, new KeyValuePair<string, object?>("stage", step));
    }

    private sealed class DelegatingDisposable : IDisposable
    {
        readonly Action _dispose;
        int _disposed;

        public DelegatingDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _dispose();
        }
    }
}

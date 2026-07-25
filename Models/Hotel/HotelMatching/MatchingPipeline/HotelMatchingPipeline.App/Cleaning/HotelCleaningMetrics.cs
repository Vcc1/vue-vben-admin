using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using System.Runtime;
using VBase.AppMetrics;

namespace HotelMatchingPipeline.App;

public sealed class HotelCleaningMetrics
{
    public const string ScopeName = "HotelMatchingPipeline";

    readonly Counter<long> _nodeCompleted;
    readonly Counter<long> _nodeSucceeded;
    readonly Counter<long> _nodeFailed;
    readonly Counter<long> _detailRequestedTotal;
    readonly Counter<long> _detailEnqueuedTotal;
    readonly Counter<long> _detailMissingTotal;
    readonly Counter<long> _detailQueryFailedTotal;
    readonly Counter<long> _refillWaitTotal;
    readonly Histogram<double> _snapshotDuration;
    readonly Histogram<double> _detailQueryDuration;
    readonly Histogram<double> _writeBatchDuration;
    readonly Histogram<double> _refillWaitDuration;
    long _idQueueBatchesCurrent;
    long _idQueueIdsCurrent;
    long _idQueueCapacity;
    long _rawQueueCurrent;
    long _rawQueueCapacity;
    long _cleanQueueCurrent;
    long _cleanQueueCapacity;
    long _readerCompleted;
    long _cleanerCompleted;
    long _writerCompleted;
    long _readerDeduplicated;
    long _cleanerSucceeded;
    long _cleanerFailed;
    long _cleanTasksCurrent;
    long _cleanParallelism;
    long _writerSucceeded;
    long _writerFailed;
    long _pipelineState;
    long _detailRequested;
    long _detailEnqueued;
    long _detailMissing;
    long _writeLastBatchSize;
    long _writeLastBatchDurationMilliseconds;
    long _refillState;
    long _refillThreshold;
    long _refillLastWaitDurationMilliseconds;
    readonly ConcurrentDictionary<string, SourceProgress> _sourceProgress = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, SnapshotProgress> _snapshotProgress = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, long> _detailLastQueryDuration = new(StringComparer.OrdinalIgnoreCase);

    public HotelCleaningMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(ScopeName).Meter;
        meter.CreateObservableGauge("hotel_cleaning_id_queue_batches_current", () => Interlocked.Read(ref _idQueueBatchesCurrent));
        meter.CreateObservableGauge("hotel_cleaning_id_queue_ids_current", () => Interlocked.Read(ref _idQueueIdsCurrent));
        meter.CreateObservableGauge("hotel_cleaning_id_queue_capacity", () => Interlocked.Read(ref _idQueueCapacity));
        meter.CreateObservableGauge("hotel_cleaning_raw_queue_current", () => Interlocked.Read(ref _rawQueueCurrent));
        meter.CreateObservableGauge("hotel_cleaning_raw_queue_capacity", () => Interlocked.Read(ref _rawQueueCapacity));
        meter.CreateObservableGauge("hotel_cleaning_clean_queue_current", () => Interlocked.Read(ref _cleanQueueCurrent));
        meter.CreateObservableGauge("hotel_cleaning_clean_queue_capacity", () => Interlocked.Read(ref _cleanQueueCapacity));
        meter.CreateObservableGauge("hotel_cleaning_reader_node_completed", () => Interlocked.Read(ref _readerCompleted));
        meter.CreateObservableGauge("hotel_cleaning_cleaner_node_completed", () => Interlocked.Read(ref _cleanerCompleted));
        meter.CreateObservableGauge("hotel_cleaning_writer_node_completed", () => Interlocked.Read(ref _writerCompleted));
        meter.CreateObservableGauge("hotel_cleaning_reader_deduplicated", () => Interlocked.Read(ref _readerDeduplicated));
        meter.CreateObservableGauge("hotel_cleaning_cleaner_succeeded", () => Interlocked.Read(ref _cleanerSucceeded));
        meter.CreateObservableGauge("hotel_cleaning_cleaner_failed", () => Interlocked.Read(ref _cleanerFailed));
        meter.CreateObservableGauge("hotel_cleaning_clean_tasks_current", () => Interlocked.Read(ref _cleanTasksCurrent));
        meter.CreateObservableGauge("hotel_cleaning_clean_parallelism", () => Interlocked.Read(ref _cleanParallelism));
        meter.CreateObservableGauge("hotel_cleaning_gc_server", () => GCSettings.IsServerGC ? 1 : 0);
        meter.CreateObservableGauge("hotel_cleaning_writer_succeeded", () => Interlocked.Read(ref _writerSucceeded));
        meter.CreateObservableGauge("hotel_cleaning_writer_failed", () => Interlocked.Read(ref _writerFailed));
        meter.CreateObservableGauge("hotel_cleaning_pipeline_state", () => Interlocked.Read(ref _pipelineState));
        meter.CreateObservableGauge("hotel_cleaning_detail_requested", () => Interlocked.Read(ref _detailRequested));
        meter.CreateObservableGauge("hotel_cleaning_detail_enqueued", () => Interlocked.Read(ref _detailEnqueued));
        meter.CreateObservableGauge("hotel_cleaning_detail_missing", () => Interlocked.Read(ref _detailMissing));
        meter.CreateObservableGauge("hotel_cleaning_write_last_batch_size", () => Interlocked.Read(ref _writeLastBatchSize));
        meter.CreateObservableGauge("hotel_cleaning_write_last_batch_duration_ms", () => Interlocked.Read(ref _writeLastBatchDurationMilliseconds));
        meter.CreateObservableGauge("hotel_cleaning_refill_state", () => Interlocked.Read(ref _refillState));
        meter.CreateObservableGauge("hotel_cleaning_refill_threshold", () => Interlocked.Read(ref _refillThreshold));
        meter.CreateObservableGauge("hotel_cleaning_refill_last_wait_duration_ms", () => Interlocked.Read(ref _refillLastWaitDurationMilliseconds));
        meter.CreateObservableGauge("hotel_cleaning_source_total", () => SourceMeasurements(static progress => progress.Total));
        meter.CreateObservableGauge("hotel_cleaning_source_cleaned", () => SourceMeasurements(static progress => progress.Cleaned));
        meter.CreateObservableGauge("hotel_cleaning_source_remaining", () => SourceMeasurements(static progress => progress.Remaining));
        meter.CreateObservableGauge("hotel_cleaning_source_failed", () => SourceMeasurements(static progress => progress.Failed));
        meter.CreateObservableGauge("hotel_cleaning_source_pending", () => SourceMeasurements(static progress => progress.Pending));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_state", () => SnapshotMeasurements(static progress => progress.State));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_total", () => SnapshotMeasurements(static progress => progress.Total));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_pending", () => SnapshotMeasurements(static progress => progress.Pending));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_already_cleaned", () => SnapshotMeasurements(static progress => progress.AlreadyCleaned));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_skipped_failed", () => SnapshotMeasurements(static progress => progress.SkippedFailed));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_skipped_total", () => SnapshotMeasurements(static progress => progress.SkippedTotal));
        meter.CreateObservableGauge("hotel_cleaning_id_snapshot_duration_ms", () => SnapshotMeasurements(static progress => progress.DurationMilliseconds));
        meter.CreateObservableGauge("hotel_cleaning_detail_last_query_duration_ms", () => DetailDurationMeasurements());

        _nodeCompleted = meter.CreateCounter<long>("hotel_cleaning_node_completed_total");
        _nodeSucceeded = meter.CreateCounter<long>("hotel_cleaning_node_succeeded_total");
        _nodeFailed = meter.CreateCounter<long>("hotel_cleaning_node_failed_total");
        _detailRequestedTotal = meter.CreateCounter<long>("hotel_cleaning_detail_requested_total");
        _detailEnqueuedTotal = meter.CreateCounter<long>("hotel_cleaning_detail_enqueued_total");
        _detailMissingTotal = meter.CreateCounter<long>("hotel_cleaning_detail_missing_total");
        _detailQueryFailedTotal = meter.CreateCounter<long>("hotel_cleaning_detail_query_failed_total");
        _refillWaitTotal = meter.CreateCounter<long>("hotel_cleaning_refill_wait_total");
        _snapshotDuration = meter.CreateHistogram<double>("hotel_cleaning_id_snapshot_query_duration_ms", unit: "ms");
        _detailQueryDuration = meter.CreateHistogram<double>("hotel_cleaning_detail_query_duration_ms", unit: "ms");
        _writeBatchDuration = meter.CreateHistogram<double>("hotel_cleaning_write_batch_duration_ms", unit: "ms");
        _refillWaitDuration = meter.CreateHistogram<double>("hotel_cleaning_refill_wait_duration_ms", unit: "ms");
    }

    public void StartRun()
    {
        Interlocked.Exchange(ref _idQueueBatchesCurrent, 0);
        Interlocked.Exchange(ref _idQueueIdsCurrent, 0);
        Interlocked.Exchange(ref _rawQueueCurrent, 0);
        Interlocked.Exchange(ref _cleanQueueCurrent, 0);
        Interlocked.Exchange(ref _readerCompleted, 0);
        Interlocked.Exchange(ref _cleanerCompleted, 0);
        Interlocked.Exchange(ref _writerCompleted, 0);
        Interlocked.Exchange(ref _readerDeduplicated, 0);
        Interlocked.Exchange(ref _cleanerSucceeded, 0);
        Interlocked.Exchange(ref _cleanerFailed, 0);
        Interlocked.Exchange(ref _cleanTasksCurrent, 0);
        Interlocked.Exchange(ref _cleanParallelism, 0);
        Interlocked.Exchange(ref _writerSucceeded, 0);
        Interlocked.Exchange(ref _writerFailed, 0);
        Interlocked.Exchange(ref _detailRequested, 0);
        Interlocked.Exchange(ref _detailEnqueued, 0);
        Interlocked.Exchange(ref _detailMissing, 0);
        Interlocked.Exchange(ref _writeLastBatchSize, 0);
        Interlocked.Exchange(ref _writeLastBatchDurationMilliseconds, 0);
        Interlocked.Exchange(ref _refillState, 0);
        Interlocked.Exchange(ref _refillThreshold, 0);
        Interlocked.Exchange(ref _refillLastWaitDurationMilliseconds, 0);
        Interlocked.Exchange(ref _pipelineState, 1);
        _sourceProgress.Clear();
        _snapshotProgress.Clear();
        _detailLastQueryDuration.Clear();
        _snapshotProgress["OTA"] = default;
        _snapshotProgress["SUP"] = default;
        _detailLastQueryDuration["OTA"] = 0;
        _detailLastQueryDuration["SUP"] = 0;
    }

    public void CompleteRun()
    {
        Interlocked.Exchange(ref _idQueueBatchesCurrent, 0);
        Interlocked.Exchange(ref _idQueueIdsCurrent, 0);
        Interlocked.Exchange(ref _rawQueueCurrent, 0);
        Interlocked.Exchange(ref _cleanQueueCurrent, 0);
        Interlocked.Exchange(ref _refillState, 0);
        Interlocked.Exchange(ref _pipelineState, 4);
    }

    public void FailRun()
    {
        Interlocked.Exchange(ref _refillState, 0);
        Interlocked.Exchange(ref _pipelineState, 9);
    }

    public void SetRefillThreshold(long threshold)
    {
        Interlocked.Exchange(ref _refillThreshold, threshold);
    }

    public void StartIdSnapshot(string source)
    {
        _snapshotProgress[source] = new SnapshotProgress(1, 0, 0, 0, 0, 0);
    }

    public void ReportIdSnapshotProgress(
        string source,
        long total,
        long pending,
        long alreadyCleaned,
        long skippedFailed,
        double elapsedMilliseconds)
    {
        _snapshotProgress[source] = new SnapshotProgress(
            1,
            total,
            pending,
            alreadyCleaned,
            skippedFailed,
            (long)elapsedMilliseconds);
    }

    public void SetSourceTotal(string source, long total)
    {
        _sourceProgress.AddOrUpdate(
            source,
            new SourceProgress(total, 0, 0),
            (_, current) => current with { Total = Math.Max(current.Total, total) });
    }

    public void CompleteIdSnapshot(
        string source,
        long total,
        long alreadyCleaned,
        long skippedFailed,
        long pending,
        double elapsedMilliseconds)
    {
        _snapshotProgress[source] = new SnapshotProgress(
            2,
            total,
            pending,
            alreadyCleaned,
            skippedFailed,
            (long)elapsedMilliseconds);
        _snapshotDuration.Record(elapsedMilliseconds, Tag("source", source), Tag("outcome", "succeeded"));
    }

    public void FailIdSnapshot(string source, double elapsedMilliseconds)
    {
        _snapshotProgress.AddOrUpdate(
            source,
            new SnapshotProgress(9, 0, 0, 0, 0, (long)elapsedMilliseconds),
            (_, current) => current with { State = 9, DurationMilliseconds = (long)elapsedMilliseconds });
        _snapshotDuration.Record(elapsedMilliseconds, Tag("source", source), Tag("outcome", "failed"));
    }

    public void RecordDetailBatch(string source, long requested, long enqueued, long missing, double elapsedMilliseconds)
    {
        Interlocked.Add(ref _detailRequested, requested);
        Interlocked.Add(ref _detailEnqueued, enqueued);
        Interlocked.Add(ref _detailMissing, missing);
        _detailRequestedTotal.Add(requested, Tag("source", source));
        _detailEnqueuedTotal.Add(enqueued, Tag("source", source));
        if (missing > 0)
        {
            _detailMissingTotal.Add(missing, Tag("source", source));
        }

        _detailLastQueryDuration[source] = (long)elapsedMilliseconds;
        _detailQueryDuration.Record(elapsedMilliseconds, Tag("source", source), Tag("outcome", "succeeded"));
    }

    public void RecordDetailQueryFailure(string source, long requested, double elapsedMilliseconds)
    {
        Interlocked.Add(ref _detailRequested, requested);
        _detailRequestedTotal.Add(requested, Tag("source", source));
        _detailQueryFailedTotal.Add(1, Tag("source", source));
        _detailLastQueryDuration[source] = (long)elapsedMilliseconds;
        _detailQueryDuration.Record(elapsedMilliseconds, Tag("source", source), Tag("outcome", "failed"));
    }

    public void RecordWriteBatch(long batchSize, double elapsedMilliseconds, bool succeeded)
    {
        Interlocked.Exchange(ref _writeLastBatchSize, batchSize);
        Interlocked.Exchange(ref _writeLastBatchDurationMilliseconds, (long)elapsedMilliseconds);
        _writeBatchDuration.Record(
            elapsedMilliseconds,
            Tag("outcome", succeeded ? "succeeded" : "failed"));
    }

    public void SetCleanTaskState(long current, long parallelism)
    {
        Interlocked.Exchange(ref _cleanTasksCurrent, current);
        Interlocked.Exchange(ref _cleanParallelism, parallelism);
    }

    public void SetRefillState(long state)
    {
        Interlocked.Exchange(ref _refillState, state);
    }

    public void RecordRefillWait(double elapsedMilliseconds)
    {
        Interlocked.Exchange(ref _refillLastWaitDurationMilliseconds, (long)elapsedMilliseconds);
        _refillWaitTotal.Add(1);
        _refillWaitDuration.Record(elapsedMilliseconds);
    }

    public void SetQueueDepths(
        long idCurrent,
        long idCapacity,
        long idBatchSize,
        long rawCurrent,
        long rawCapacity,
        long cleanCurrent,
        long cleanCapacity)
    {
        Interlocked.Exchange(ref _idQueueIdsCurrent, idCurrent);
        Interlocked.Exchange(ref _idQueueCapacity, idCapacity);
        Interlocked.Exchange(
            ref _idQueueBatchesCurrent,
            idCurrent <= 0 ? 0 : (idCurrent + Math.Max(1, idBatchSize) - 1) / Math.Max(1, idBatchSize));
        Interlocked.Exchange(ref _rawQueueCurrent, rawCurrent);
        Interlocked.Exchange(ref _rawQueueCapacity, rawCapacity);
        Interlocked.Exchange(ref _cleanQueueCurrent, cleanCurrent);
        Interlocked.Exchange(ref _cleanQueueCapacity, cleanCapacity);
    }

    public void SetNodeCompleted(long reader, long cleaner, long writer)
    {
        Interlocked.Exchange(ref _readerCompleted, reader);
        Interlocked.Exchange(ref _cleanerCompleted, cleaner);
        Interlocked.Exchange(ref _writerCompleted, writer);
    }

    public void SetSourceProgress(string source, long total, long cleaned, long failed)
    {
        _sourceProgress[source] = new SourceProgress(total, cleaned, failed);
    }

    public void AddReader(long completed, long deduplicated, string source)
    {
        Interlocked.Add(ref _readerCompleted, completed);
        Interlocked.Add(ref _readerDeduplicated, deduplicated);
        _nodeCompleted.Add(completed, Tag("node", "reader"), Tag("source", source));
    }

    public void AddCleaner(long completed, long succeeded, long failed, string source)
    {
        Interlocked.Add(ref _cleanerCompleted, completed);
        Interlocked.Add(ref _cleanerSucceeded, succeeded);
        Interlocked.Add(ref _cleanerFailed, failed);
        _nodeCompleted.Add(completed, Tag("node", "cleaner"), Tag("source", source));
        if (succeeded > 0)
        {
            _nodeSucceeded.Add(succeeded, Tag("node", "cleaner"), Tag("source", source));
        }

        if (failed > 0)
        {
            _nodeFailed.Add(failed, Tag("node", "cleaner"), Tag("source", source));
        }
    }

    public void AddWriter(long completed, long succeeded, long failed, string source)
    {
        Interlocked.Add(ref _writerCompleted, completed);
        Interlocked.Add(ref _writerSucceeded, succeeded);
        Interlocked.Add(ref _writerFailed, failed);
        _nodeCompleted.Add(completed, Tag("node", "writer"), Tag("source", source));
        if (succeeded > 0)
        {
            _nodeSucceeded.Add(succeeded, Tag("node", "writer"), Tag("source", source));
        }

        if (failed > 0)
        {
            _nodeFailed.Add(failed, Tag("node", "writer"), Tag("source", source));
        }
    }

    static KeyValuePair<string, object?> Tag(string key, string value)
        => new(key, value);

    IEnumerable<Measurement<long>> SourceMeasurements(Func<SourceProgress, long> selector)
    {
        foreach (var item in _sourceProgress)
        {
            yield return new Measurement<long>(
                selector(item.Value),
                Tag("source", item.Key));
        }
    }

    IEnumerable<Measurement<long>> SnapshotMeasurements(Func<SnapshotProgress, long> selector)
    {
        foreach (var item in _snapshotProgress)
        {
            yield return new Measurement<long>(
                selector(item.Value),
                Tag("source", item.Key));
        }
    }

    IEnumerable<Measurement<long>> DetailDurationMeasurements()
    {
        foreach (var item in _detailLastQueryDuration)
        {
            yield return new Measurement<long>(item.Value, Tag("source", item.Key));
        }
    }

    readonly record struct SourceProgress(long Total, long Cleaned, long Failed)
    {
        public long Remaining => Math.Max(0, Total - Cleaned);

        public long Pending => Math.Max(0, Remaining - Failed);
    }

    readonly record struct SnapshotProgress(
        long State,
        long Total,
        long Pending,
        long AlreadyCleaned,
        long SkippedFailed,
        long DurationMilliseconds)
    {
        public long SkippedTotal => AlreadyCleaned + SkippedFailed;
    }
}

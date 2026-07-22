using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using VBase.AppMetrics;

namespace HotelMatchingPipeline.App;

public sealed class HotelCleaningMetrics
{
    public const string ScopeName = "HotelMatchingPipeline";

    readonly Counter<long> _nodeCompleted;
    readonly Counter<long> _nodeSucceeded;
    readonly Counter<long> _nodeFailed;
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
    long _writerSucceeded;
    long _writerFailed;
    long _pipelineState;
    readonly ConcurrentDictionary<string, SourceProgress> _sourceProgress = new(StringComparer.OrdinalIgnoreCase);

    public HotelCleaningMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(ScopeName).Meter;
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
        meter.CreateObservableGauge("hotel_cleaning_writer_succeeded", () => Interlocked.Read(ref _writerSucceeded));
        meter.CreateObservableGauge("hotel_cleaning_writer_failed", () => Interlocked.Read(ref _writerFailed));
        meter.CreateObservableGauge("hotel_cleaning_pipeline_state", () => Interlocked.Read(ref _pipelineState));
        meter.CreateObservableGauge("hotel_cleaning_source_total", () => SourceMeasurements(static progress => progress.Total));
        meter.CreateObservableGauge("hotel_cleaning_source_cleaned", () => SourceMeasurements(static progress => progress.Cleaned));
        meter.CreateObservableGauge("hotel_cleaning_source_remaining", () => SourceMeasurements(static progress => progress.Remaining));
        meter.CreateObservableGauge("hotel_cleaning_source_failed", () => SourceMeasurements(static progress => progress.Failed));
        meter.CreateObservableGauge("hotel_cleaning_source_pending", () => SourceMeasurements(static progress => progress.Pending));

        _nodeCompleted = meter.CreateCounter<long>("hotel_cleaning_node_completed_total");
        _nodeSucceeded = meter.CreateCounter<long>("hotel_cleaning_node_succeeded_total");
        _nodeFailed = meter.CreateCounter<long>("hotel_cleaning_node_failed_total");
    }

    public void StartRun()
    {
        Interlocked.Exchange(ref _rawQueueCurrent, 0);
        Interlocked.Exchange(ref _cleanQueueCurrent, 0);
        Interlocked.Exchange(ref _readerCompleted, 0);
        Interlocked.Exchange(ref _cleanerCompleted, 0);
        Interlocked.Exchange(ref _writerCompleted, 0);
        Interlocked.Exchange(ref _readerDeduplicated, 0);
        Interlocked.Exchange(ref _cleanerSucceeded, 0);
        Interlocked.Exchange(ref _cleanerFailed, 0);
        Interlocked.Exchange(ref _writerSucceeded, 0);
        Interlocked.Exchange(ref _writerFailed, 0);
        Interlocked.Exchange(ref _pipelineState, 1);
        _sourceProgress.Clear();
    }

    public void CompleteRun()
    {
        Interlocked.Exchange(ref _rawQueueCurrent, 0);
        Interlocked.Exchange(ref _cleanQueueCurrent, 0);
        Interlocked.Exchange(ref _pipelineState, 4);
    }

    public void FailRun()
    {
        Interlocked.Exchange(ref _pipelineState, 9);
    }

    public void SetQueueDepths(long rawCurrent, long rawCapacity, long cleanCurrent, long cleanCapacity)
    {
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

    readonly record struct SourceProgress(long Total, long Cleaned, long Failed)
    {
        public long Remaining => Math.Max(0, Total - Cleaned);

        public long Pending => Math.Max(0, Remaining - Failed);
    }
}

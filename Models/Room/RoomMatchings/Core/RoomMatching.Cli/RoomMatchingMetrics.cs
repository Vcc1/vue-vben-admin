using System.Diagnostics.Metrics;
using OTADomain.Matchings;
using VBase.AppMetrics;

public sealed class RoomMatchingMetrics
{
    private const string ScopeName = "RoomMatching";
    private const string ProgressLoop = "roommatching_hotels";

    private readonly Counter<long> roundTotal;
    private readonly Counter<long> matchResultTotal;
    private readonly Counter<long> errorTotal;
    private readonly Counter<long> redisPushTotal;
    private readonly Histogram<double> readDurationMs;
    private readonly Histogram<double> matchDurationMs;
    private readonly Histogram<double> writeDurationMs;

    private long currentRoundId;
    private long workerState;
    private long currentHotelsTotal;
    private long currentHotelsRead;
    private long currentHotelsMatched;
    private long currentHotelsWritten;
    private long currentRoomsGenerated;
    private long currentRoomsPersisted;
    private long readQueueDepth;
    private long writeQueueDepth;
    private long activeMatchWorkers;
    private long lastBatchDurationMs;
    private long lastWriteDurationMs;
    private long lastErrorTimestamp;

    public RoomMatchingMetrics(IMetricsFactory metricsFactory)
    {
        var meter = metricsFactory.Create(ScopeName).Meter;

        meter.CreateObservableGauge("room_matching_current_round_id", () => Interlocked.Read(ref currentRoundId));
        meter.CreateObservableGauge("room_matching_worker_state", () => Interlocked.Read(ref workerState));
        meter.CreateObservableGauge("room_matching_current_hotels_total", () => Interlocked.Read(ref currentHotelsTotal));
        meter.CreateObservableGauge("room_matching_current_hotels_read", () => Interlocked.Read(ref currentHotelsRead));
        meter.CreateObservableGauge("room_matching_current_hotels_matched", () => Interlocked.Read(ref currentHotelsMatched));
        meter.CreateObservableGauge("room_matching_current_hotels_written", () => Interlocked.Read(ref currentHotelsWritten));
        meter.CreateObservableGauge("room_matching_current_rooms_generated", () => Interlocked.Read(ref currentRoomsGenerated));
        meter.CreateObservableGauge("room_matching_current_rooms_persisted", () => Interlocked.Read(ref currentRoomsPersisted));
        meter.CreateObservableGauge("room_matching_read_queue_depth", () => Interlocked.Read(ref readQueueDepth));
        meter.CreateObservableGauge("room_matching_write_queue_depth", () => Interlocked.Read(ref writeQueueDepth));
        meter.CreateObservableGauge("room_matching_active_match_workers", () => Interlocked.Read(ref activeMatchWorkers));
        meter.CreateObservableGauge("room_matching_last_batch_duration_ms", () => Interlocked.Read(ref lastBatchDurationMs));
        meter.CreateObservableGauge("room_matching_last_write_duration_ms", () => Interlocked.Read(ref lastWriteDurationMs));
        meter.CreateObservableGauge("room_matching_last_error_timestamp", () => Interlocked.Read(ref lastErrorTimestamp));

        roundTotal = meter.CreateCounter<long>("room_matching_round_total");
        matchResultTotal = meter.CreateCounter<long>("room_matching_match_result_total");
        errorTotal = meter.CreateCounter<long>("room_matching_error_total");
        redisPushTotal = meter.CreateCounter<long>("room_matching_redis_push_total");
        readDurationMs = meter.CreateHistogram<double>("room_matching_read_duration_ms", "ms");
        matchDurationMs = meter.CreateHistogram<double>("room_matching_match_duration_ms", "ms");
        writeDurationMs = meter.CreateHistogram<double>("room_matching_write_duration_ms", "ms");

        SetWaiting();
    }

    public void StartRound()
    {
        Interlocked.Increment(ref currentRoundId);
        Interlocked.Exchange(ref currentHotelsTotal, 0);
        Interlocked.Exchange(ref currentHotelsRead, 0);
        Interlocked.Exchange(ref currentHotelsMatched, 0);
        Interlocked.Exchange(ref currentHotelsWritten, 0);
        Interlocked.Exchange(ref currentRoomsGenerated, 0);
        Interlocked.Exchange(ref currentRoomsPersisted, 0);
        Interlocked.Exchange(ref readQueueDepth, 0);
        Interlocked.Exchange(ref writeQueueDepth, 0);
        Interlocked.Exchange(ref activeMatchWorkers, 0);
        Interlocked.Exchange(ref lastBatchDurationMs, 0);
        Interlocked.Exchange(ref lastWriteDurationMs, 0);
        SetReading();
        roundTotal.Add(1, Tag("outcome", "started"));
        Telemetry.Progress.Register(ProgressLoop, total: 0);
    }

    public void CompleteRound()
    {
        SetWaiting();
        SetQueueDepths(0, 0);
        Interlocked.Exchange(ref activeMatchWorkers, 0);
        roundTotal.Add(1, Tag("outcome", "completed"));
    }

    public void FailRound(string stage)
    {
        SetError();
        RecordError(stage);
        roundTotal.Add(1, Tag("outcome", "error"));
    }

    public void SetReading() => Interlocked.Exchange(ref workerState, 1);

    public void SetMatching() => Interlocked.Exchange(ref workerState, 2);

    public void SetWriting() => Interlocked.Exchange(ref workerState, 3);

    public void SetWaiting() => Interlocked.Exchange(ref workerState, 4);

    public void SetError() => Interlocked.Exchange(ref workerState, 9);

    public void SetHotelTotal(long total)
    {
        Interlocked.Exchange(ref currentHotelsTotal, total);
        Telemetry.Progress.Update(ProgressLoop, current: Interlocked.Read(ref currentHotelsMatched), total: total);
    }

    public void AddHotelsRead(long count)
    {
        Interlocked.Add(ref currentHotelsRead, count);
    }

    public void AddHotelsMatched(long count)
    {
        var current = Interlocked.Add(ref currentHotelsMatched, count);
        Telemetry.Progress.Update(ProgressLoop, current: current, total: Interlocked.Read(ref currentHotelsTotal));
    }

    public void AddHotelsWritten(long count)
    {
        Interlocked.Add(ref currentHotelsWritten, count);
    }

    public void AddRoomsGenerated(long count)
    {
        Interlocked.Add(ref currentRoomsGenerated, count);
    }

    public void AddRoomsPersisted(long count)
    {
        Interlocked.Add(ref currentRoomsPersisted, count);
    }

    public void SetQueueDepths(long readDepth, long writeDepth)
    {
        Interlocked.Exchange(ref readQueueDepth, readDepth);
        Interlocked.Exchange(ref writeQueueDepth, writeDepth);
    }

    public void IncrementActiveMatchWorkers()
    {
        Interlocked.Increment(ref activeMatchWorkers);
    }

    public void DecrementActiveMatchWorkers()
    {
        Interlocked.Decrement(ref activeMatchWorkers);
    }

    public void RecordReadDuration(string source, long elapsedMs)
    {
        readDurationMs.Record(elapsedMs, Tag("source", source));
        Interlocked.Exchange(ref lastBatchDurationMs, elapsedMs);
    }

    public void RecordMatchDuration(long elapsedMs)
    {
        matchDurationMs.Record(elapsedMs);
        Interlocked.Exchange(ref lastBatchDurationMs, elapsedMs);
    }

    public void RecordWriteDuration(string step, long elapsedMs)
    {
        writeDurationMs.Record(elapsedMs, Tag("step", step));
        Interlocked.Exchange(ref lastWriteDurationMs, elapsedMs);
    }

    public void RecordMatchResult(RoomMatchStatus status, long count)
    {
        matchResultTotal.Add(count, Tag("status", status.ToString()));
    }

    public void RecordRedisPush(string outcome, long count)
    {
        redisPushTotal.Add(count, Tag("outcome", outcome));
    }

    public void RecordError(string stage)
    {
        errorTotal.Add(1, Tag("stage", stage));
        Interlocked.Exchange(ref lastErrorTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static KeyValuePair<string, object?> Tag(string key, string value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }
}

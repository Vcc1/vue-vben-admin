using System.Diagnostics.Metrics;
using VBase.AppMetrics;

namespace HotelMatchingPipeline.App;

public readonly record struct HotelMatchingProgressSnapshot(
    long InformationCompleted,
    long InformationActive,
    long InformationSlowCount,
    long InformationSlowestSupHotelId,
    long InformationSlowestElapsedMilliseconds,
    long InformationRetrying,
    long InformationRetryCount,
    long InformationRetrySupHotelId,
    long MatcherCompleted,
    long WriterCompleted,
    long WriteLastBatchDurationMilliseconds);

public sealed class HotelMatchingMetrics
{
    public const string ScopeName = HotelCleaningMetrics.ScopeName;

    readonly Counter<long> _candidateTotal;
    readonly Counter<long> _nodeCompletedTotal;
    readonly Counter<long> _nodeFailedTotal;
    readonly Counter<long> _nodeSucceededTotal;
    readonly Histogram<double> _nodeDuration;
    readonly Histogram<double> _roundDuration;
    long _ambiguousHotels;
    long _candidateCount;
    long _idFailed;
    long _idQueueCapacity;
    long _idQueueCurrent;
    long _idsRecalled;
    long _informationCompleted;
    long _informationFailed;
    long _informationActive;
    long _informationSlowCount;
    long _informationSlowestSupHotelId;
    long _informationSlowestElapsedMilliseconds;
    long _informationParallelism;
    long _informationLastDurationMilliseconds;
    long _informationLastFailedSupHotelId;
    long _informationLastRetryDelayMilliseconds;
    long _informationMissing;
    long _informationRetryCount;
    long _informationRetrySupHotelId;
    long _informationRetrying;
    long _informationSucceeded;
    long _lastRoundDurationMilliseconds;
    long _matchDistanceMeters;
    long _matcherCompleted;
    long _matcherFailed;
    long _matcherLastDurationMilliseconds;
    long _matchedHotels;
    long _maxCandidates;
    long _noMatchHotels;
    long _pipelineState;
    long _recallRadiusMeters;
    long _resultQueueCapacity;
    long _resultQueueCurrent;
    long _roundNumber;
    long _singleCandidateHotels;
    long _workQueueCapacity;
    long _workQueueCurrent;
    long _writeBatchSize;
    long _writeLastBatchDurationMilliseconds;
    long _writeLastBatchSize;
    long _writerCompleted;
    long _writerFailed;
    long _writerSucceeded;

    public HotelMatchingMetrics(IMetricsFactory metricsFactory)
        : this(metricsFactory.Create(ScopeName).Meter)
    {
    }

    public HotelMatchingMetrics()
        : this(new Meter($"{ScopeName}.Tests"))
    {
    }

    HotelMatchingMetrics(Meter meter)
    {
        meter.CreateObservableGauge("hotel_matching_pipeline_state", () => Read(ref _pipelineState));
        meter.CreateObservableGauge("hotel_matching_round_number", () => Read(ref _roundNumber));
        meter.CreateObservableGauge(
            "hotel_matching_round_last_duration_ms",
            () => Read(ref _lastRoundDurationMilliseconds));
        meter.CreateObservableGauge("hotel_matching_id_queue_current", () => Read(ref _idQueueCurrent));
        meter.CreateObservableGauge("hotel_matching_id_queue_capacity", () => Read(ref _idQueueCapacity));
        meter.CreateObservableGauge("hotel_matching_work_queue_current", () => Read(ref _workQueueCurrent));
        meter.CreateObservableGauge("hotel_matching_work_queue_capacity", () => Read(ref _workQueueCapacity));
        meter.CreateObservableGauge("hotel_matching_result_queue_current", () => Read(ref _resultQueueCurrent));
        meter.CreateObservableGauge("hotel_matching_result_queue_capacity", () => Read(ref _resultQueueCapacity));
        meter.CreateObservableGauge("hotel_matching_ids_recalled", () => Read(ref _idsRecalled));
        meter.CreateObservableGauge("hotel_matching_id_failed", () => Read(ref _idFailed));
        meter.CreateObservableGauge(
            "hotel_matching_information_completed",
            () => Read(ref _informationCompleted));
        meter.CreateObservableGauge(
            "hotel_matching_information_active",
            () => Read(ref _informationActive));
        meter.CreateObservableGauge(
            "hotel_matching_information_slow_count",
            () => Read(ref _informationSlowCount));
        meter.CreateObservableGauge(
            "hotel_matching_information_slowest_sup_hotel_id",
            () => Read(ref _informationSlowestSupHotelId));
        meter.CreateObservableGauge(
            "hotel_matching_information_slowest_elapsed_ms",
            () => Read(ref _informationSlowestElapsedMilliseconds));
        meter.CreateObservableGauge(
            "hotel_matching_information_succeeded",
            () => Read(ref _informationSucceeded));
        meter.CreateObservableGauge(
            "hotel_matching_information_missing",
            () => Read(ref _informationMissing));
        meter.CreateObservableGauge(
            "hotel_matching_information_failed",
            () => Read(ref _informationFailed));
        meter.CreateObservableGauge(
            "hotel_matching_information_parallelism",
            () => Read(ref _informationParallelism));
        meter.CreateObservableGauge(
            "hotel_matching_information_last_duration_ms",
            () => Read(ref _informationLastDurationMilliseconds));
        meter.CreateObservableGauge(
            "hotel_matching_information_retrying",
            () => Read(ref _informationRetrying));
        meter.CreateObservableGauge(
            "hotel_matching_information_retry_count",
            () => Read(ref _informationRetryCount));
        meter.CreateObservableGauge(
            "hotel_matching_information_retry_sup_hotel_id",
            () => Read(ref _informationRetrySupHotelId));
        meter.CreateObservableGauge(
            "hotel_matching_information_last_failed_sup_hotel_id",
            () => Read(ref _informationLastFailedSupHotelId));
        meter.CreateObservableGauge(
            "hotel_matching_information_last_retry_delay_ms",
            () => Read(ref _informationLastRetryDelayMilliseconds));
        meter.CreateObservableGauge(
            "hotel_matching_matcher_completed",
            () => Read(ref _matcherCompleted));
        meter.CreateObservableGauge("hotel_matching_matcher_failed", () => Read(ref _matcherFailed));
        meter.CreateObservableGauge(
            "hotel_matching_matcher_last_duration_ms",
            () => Read(ref _matcherLastDurationMilliseconds));
        meter.CreateObservableGauge("hotel_matching_matched_hotels", () => Read(ref _matchedHotels));
        meter.CreateObservableGauge(
            "hotel_matching_single_candidate_hotels",
            () => Read(ref _singleCandidateHotels));
        meter.CreateObservableGauge(
            "hotel_matching_ambiguous_hotels",
            () => Read(ref _ambiguousHotels));
        meter.CreateObservableGauge("hotel_matching_no_match_hotels", () => Read(ref _noMatchHotels));
        meter.CreateObservableGauge("hotel_matching_candidate_count", () => Read(ref _candidateCount));
        meter.CreateObservableGauge(
            "hotel_matching_writer_completed",
            () => Read(ref _writerCompleted));
        meter.CreateObservableGauge(
            "hotel_matching_writer_succeeded",
            () => Read(ref _writerSucceeded));
        meter.CreateObservableGauge("hotel_matching_writer_failed", () => Read(ref _writerFailed));
        meter.CreateObservableGauge(
            "hotel_matching_write_last_batch_size",
            () => Read(ref _writeLastBatchSize));
        meter.CreateObservableGauge(
            "hotel_matching_write_last_batch_duration_ms",
            () => Read(ref _writeLastBatchDurationMilliseconds));
        meter.CreateObservableGauge(
            "hotel_matching_recall_radius_meters",
            () => Read(ref _recallRadiusMeters));
        meter.CreateObservableGauge(
            "hotel_matching_match_distance_meters",
            () => Read(ref _matchDistanceMeters));
        meter.CreateObservableGauge(
            "hotel_matching_max_candidates",
            () => Read(ref _maxCandidates));
        meter.CreateObservableGauge(
            "hotel_matching_write_batch_size",
            () => Read(ref _writeBatchSize));

        _nodeCompletedTotal = meter.CreateCounter<long>("hotel_matching_node_completed_total");
        _nodeSucceededTotal = meter.CreateCounter<long>("hotel_matching_node_succeeded_total");
        _nodeFailedTotal = meter.CreateCounter<long>("hotel_matching_node_failed_total");
        _candidateTotal = meter.CreateCounter<long>("hotel_matching_candidate_total");
        _nodeDuration = meter.CreateHistogram<double>("hotel_matching_node_duration_ms", unit: "ms");
        _roundDuration = meter.CreateHistogram<double>("hotel_matching_round_duration_ms", unit: "ms");
    }

    public void StartRound(
        int idQueueCapacity,
        int queueCapacity,
        int recallRadiusMeters,
        int matchDistanceMeters,
        int maxCandidates,
        int writeBatchSize,
        int informationParallelism)
    {
        Interlocked.Increment(ref _roundNumber);
        Reset(ref _idQueueCurrent);
        Reset(ref _workQueueCurrent);
        Reset(ref _resultQueueCurrent);
        Reset(ref _idsRecalled);
        Reset(ref _idFailed);
        Reset(ref _informationCompleted);
        Reset(ref _informationActive);
        Reset(ref _informationSlowCount);
        Reset(ref _informationSlowestSupHotelId);
        Reset(ref _informationSlowestElapsedMilliseconds);
        Reset(ref _informationSucceeded);
        Reset(ref _informationMissing);
        Reset(ref _informationFailed);
        Reset(ref _informationLastDurationMilliseconds);
        Reset(ref _informationLastFailedSupHotelId);
        Reset(ref _informationLastRetryDelayMilliseconds);
        Reset(ref _informationRetryCount);
        Reset(ref _informationRetrySupHotelId);
        Reset(ref _informationRetrying);
        Reset(ref _matcherCompleted);
        Reset(ref _matcherFailed);
        Reset(ref _matcherLastDurationMilliseconds);
        Reset(ref _matchedHotels);
        Reset(ref _singleCandidateHotels);
        Reset(ref _ambiguousHotels);
        Reset(ref _noMatchHotels);
        Reset(ref _candidateCount);
        Reset(ref _writerCompleted);
        Reset(ref _writerSucceeded);
        Reset(ref _writerFailed);
        Reset(ref _writeLastBatchSize);
        Reset(ref _writeLastBatchDurationMilliseconds);
        Interlocked.Exchange(ref _idQueueCapacity, idQueueCapacity);
        Interlocked.Exchange(ref _workQueueCapacity, queueCapacity);
        Interlocked.Exchange(ref _resultQueueCapacity, queueCapacity);
        Interlocked.Exchange(ref _recallRadiusMeters, recallRadiusMeters);
        Interlocked.Exchange(ref _matchDistanceMeters, matchDistanceMeters);
        Interlocked.Exchange(ref _maxCandidates, maxCandidates);
        Interlocked.Exchange(ref _writeBatchSize, writeBatchSize);
        Interlocked.Exchange(ref _informationParallelism, informationParallelism);
        Interlocked.Exchange(ref _pipelineState, 1);
    }

    public void CompleteRound(double elapsedMilliseconds)
    {
        SetRoundDuration(elapsedMilliseconds, "succeeded");
        Reset(ref _idQueueCurrent);
        Reset(ref _workQueueCurrent);
        Reset(ref _resultQueueCurrent);
        Interlocked.Exchange(ref _pipelineState, 4);
    }

    public void FailRound(double elapsedMilliseconds)
    {
        SetRoundDuration(elapsedMilliseconds, "failed");
        Interlocked.Exchange(ref _pipelineState, 9);
    }

    public void EnqueueId()
    {
        Interlocked.Increment(ref _idQueueCurrent);
        Interlocked.Increment(ref _idsRecalled);
        AddNode(_nodeCompletedTotal, "id", 1);
        AddNode(_nodeSucceededTotal, "id", 1);
    }

    public void DequeueId()
        => DecrementNonNegative(ref _idQueueCurrent);

    public void SetIdQueueDepth(long current)
        => Interlocked.Exchange(ref _idQueueCurrent, Math.Max(0, current));

    public void RecordIdFailure()
    {
        Interlocked.Increment(ref _idFailed);
        AddNode(_nodeFailedTotal, "id", 1);
    }

    public void EnqueueWork()
        => Interlocked.Increment(ref _workQueueCurrent);

    public void DequeueWork()
        => DecrementNonNegative(ref _workQueueCurrent);

    public void SetWorkQueueDepth(long current)
        => Interlocked.Exchange(ref _workQueueCurrent, Math.Max(0, current));

    public void RecordInformation(double elapsedMilliseconds, bool loaded)
    {
        Interlocked.Increment(ref _informationCompleted);
        Interlocked.Exchange(ref _informationLastDurationMilliseconds, (long)elapsedMilliseconds);
        AddNode(_nodeCompletedTotal, "information", 1);
        if (loaded)
        {
            Interlocked.Increment(ref _informationSucceeded);
            AddNode(_nodeSucceededTotal, "information", 1);
        }
        else
        {
            Interlocked.Increment(ref _informationMissing);
            AddNode(_nodeFailedTotal, "information", 1);
        }
        RecordNodeDuration("information", elapsedMilliseconds, loaded ? "succeeded" : "missing");
    }

    public void RecordInformationLoadStarted()
        => Interlocked.Increment(ref _informationActive);

    public void RecordInformationLoadCompleted()
        => DecrementNonNegative(ref _informationActive);

    public void RecordInformationActiveSnapshot(
        long active,
        long slowCount,
        int slowestSupHotelId,
        long slowestElapsedMilliseconds)
    {
        Interlocked.Exchange(ref _informationActive, Math.Max(0, active));
        Interlocked.Exchange(ref _informationSlowCount, Math.Max(0, slowCount));
        Interlocked.Exchange(ref _informationSlowestSupHotelId, slowestSupHotelId);
        Interlocked.Exchange(
            ref _informationSlowestElapsedMilliseconds,
            Math.Max(0, slowestElapsedMilliseconds));
    }

    public void RecordInformationRetry(
        int supHotelId,
        double elapsedMilliseconds,
        int delayMilliseconds,
        bool startedRetrying)
    {
        Interlocked.Increment(ref _informationFailed);
        Interlocked.Increment(ref _informationRetryCount);
        if (startedRetrying)
        {
            Interlocked.Increment(ref _informationRetrying);
        }
        Interlocked.Exchange(ref _informationRetrySupHotelId, supHotelId);
        Interlocked.Exchange(ref _informationLastFailedSupHotelId, supHotelId);
        Interlocked.Exchange(ref _informationLastRetryDelayMilliseconds, delayMilliseconds);
        Interlocked.Exchange(ref _informationLastDurationMilliseconds, (long)elapsedMilliseconds);
        AddNode(_nodeFailedTotal, "information", 1);
        RecordNodeDuration("information", elapsedMilliseconds, "retry");
    }

    public void RecordInformationRetryCompleted()
    {
        while (true)
        {
            var current = Interlocked.Read(ref _informationRetrying);
            if (current <= 0)
            {
                Reset(ref _informationRetrySupHotelId);
                return;
            }

            if (Interlocked.CompareExchange(ref _informationRetrying, current - 1, current) == current)
            {
                if (current == 1)
                {
                    Reset(ref _informationRetrySupHotelId);
                }
                return;
            }
        }
    }

    public void EnqueueResult()
        => Interlocked.Increment(ref _resultQueueCurrent);

    public void DequeueResult()
        => DecrementNonNegative(ref _resultQueueCurrent);

    public void SetResultQueueDepth(long current)
        => Interlocked.Exchange(ref _resultQueueCurrent, Math.Max(0, current));

    public void RecordMatch(int candidateCount, double elapsedMilliseconds)
    {
        Interlocked.Increment(ref _matcherCompleted);
        Interlocked.Add(ref _candidateCount, candidateCount);
        Interlocked.Exchange(ref _matcherLastDurationMilliseconds, (long)elapsedMilliseconds);
        AddNode(_nodeCompletedTotal, "matcher", 1);
        AddNode(_nodeSucceededTotal, "matcher", 1);
        if (candidateCount == 0)
        {
            Interlocked.Increment(ref _noMatchHotels);
        }
        else
        {
            Interlocked.Increment(ref _matchedHotels);
            if (candidateCount == 1)
            {
                Interlocked.Increment(ref _singleCandidateHotels);
            }
            else
            {
                Interlocked.Increment(ref _ambiguousHotels);
            }
            _candidateTotal.Add(candidateCount);
        }
        RecordNodeDuration("matcher", elapsedMilliseconds, "succeeded");
    }

    public void RecordMatchFailure(double elapsedMilliseconds)
    {
        Interlocked.Increment(ref _matcherCompleted);
        Interlocked.Increment(ref _matcherFailed);
        Interlocked.Exchange(ref _matcherLastDurationMilliseconds, (long)elapsedMilliseconds);
        AddNode(_nodeCompletedTotal, "matcher", 1);
        AddNode(_nodeFailedTotal, "matcher", 1);
        RecordNodeDuration("matcher", elapsedMilliseconds, "failed");
    }

    public void RecordWrite(
        int hotelCount,
        int candidateCount,
        double elapsedMilliseconds,
        bool succeeded)
    {
        Interlocked.Add(ref _writerCompleted, hotelCount);
        Interlocked.Exchange(ref _writeLastBatchSize, hotelCount);
        Interlocked.Exchange(ref _writeLastBatchDurationMilliseconds, (long)elapsedMilliseconds);
        AddNode(_nodeCompletedTotal, "writer", hotelCount);
        if (succeeded)
        {
            Interlocked.Add(ref _writerSucceeded, hotelCount);
            AddNode(_nodeSucceededTotal, "writer", hotelCount);
        }
        else
        {
            Interlocked.Add(ref _writerFailed, hotelCount);
            AddNode(_nodeFailedTotal, "writer", hotelCount);
        }
        RecordNodeDuration(
            "writer",
            elapsedMilliseconds,
            succeeded ? "succeeded" : "failed",
            ("candidate_count", candidateCount));
    }

    void SetRoundDuration(double elapsedMilliseconds, string outcome)
    {
        Interlocked.Exchange(ref _lastRoundDurationMilliseconds, (long)elapsedMilliseconds);
        _roundDuration.Record(elapsedMilliseconds, Tag("outcome", outcome));
    }

    void RecordNodeDuration(
        string node,
        double elapsedMilliseconds,
        string outcome,
        (string Key, int Value)? extraTag = null)
    {
        if (extraTag is { } extra)
        {
            _nodeDuration.Record(
                elapsedMilliseconds,
                Tag("node", node),
                Tag("outcome", outcome),
                new KeyValuePair<string, object?>(extra.Key, extra.Value));
            return;
        }
        _nodeDuration.Record(
            elapsedMilliseconds,
            Tag("node", node),
            Tag("outcome", outcome));
    }

    static void AddNode(Counter<long> counter, string node, long value)
        => counter.Add(value, Tag("node", node));

    static void DecrementNonNegative(ref long target)
    {
        while (true)
        {
            var current = Interlocked.Read(ref target);
            if (current <= 0 || Interlocked.CompareExchange(ref target, current - 1, current) == current)
            {
                return;
            }
        }
    }

    static long Read(ref long target)
        => Interlocked.Read(ref target);

    static void Reset(ref long target)
        => Interlocked.Exchange(ref target, 0);

    static KeyValuePair<string, object?> Tag(string key, string value)
        => new(key, value);

    public HotelMatchingProgressSnapshot Snapshot()
        => new(
            Read(ref _informationCompleted),
            Read(ref _informationActive),
            Read(ref _informationSlowCount),
            Read(ref _informationSlowestSupHotelId),
            Read(ref _informationSlowestElapsedMilliseconds),
            Read(ref _informationRetrying),
            Read(ref _informationRetryCount),
            Read(ref _informationRetrySupHotelId),
            Read(ref _matcherCompleted),
            Read(ref _writerCompleted),
            Read(ref _writeLastBatchDurationMilliseconds));
}

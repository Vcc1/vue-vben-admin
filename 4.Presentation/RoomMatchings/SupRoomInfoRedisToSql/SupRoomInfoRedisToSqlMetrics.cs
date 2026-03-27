using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using VBase.AppMetrics;

public sealed class SupRoomInfoRedisToSqlMetrics
{
    readonly Counter<long> _redisFetchTotal;
    readonly Counter<long> _tempInsertTotal;
    readonly Counter<long> _batchTotal;
    readonly Histogram<long> _batchSize;
    readonly ObservableGauge<long> _channelDepthGauge;
    long _channelDepth;

    public SupRoomInfoRedisToSqlMetrics(DefaultMetrics metrics)
    {
        var meter = metrics.Meter;
        _redisFetchTotal = meter.CreateCounter<long>("sup_room_info_redis_fetch_total", unit: "rooms");
        _tempInsertTotal = meter.CreateCounter<long>("sup_room_info_temp_insert_total", unit: "rooms");
        _batchTotal = meter.CreateCounter<long>("sup_room_info_sync_batch_total", unit: "batches");
        _batchSize = meter.CreateHistogram<long>("sup_room_info_batch_size", unit: "rooms");
        _channelDepthGauge = meter.CreateObservableGauge("sup_room_info_channel_depth", () => Volatile.Read(ref _channelDepth), unit: "rooms");
    }

    public IDisposable MeasureRedisFetch() => Telemetry.Measure("sup_room_info_redis_fetch");

    public IDisposable MeasureWriteDb() => Telemetry.Measure("sup_room_info_write_db");

    public IDisposable MeasureSyncStoredProcedure() => Telemetry.Measure("sup_room_info_sync_stored_procedure");

    public IDisposable MeasureTempTableCleanup() => Telemetry.Measure("sup_room_info_temp_table_cleanup");

    public void RecordFetched(int count)
    {
        if (count > 0)
        {
            _redisFetchTotal.Add(count);
        }
    }

    public void RecordBatch(int batchSize, int insertedCount)
    {
        _batchTotal.Add(1);

        if (batchSize > 0)
        {
            _batchSize.Record(batchSize);
        }

        if (insertedCount > 0)
        {
            _tempInsertTotal.Add(insertedCount);
        }
    }

    public void RecordError(string stage)
    {
        Telemetry.Errors.Add(1, new KeyValuePair<string, object?>("stage", stage));
    }

    public void IncreaseQueueDepth(int delta = 1)
    {
        if (delta > 0)
        {
            Interlocked.Add(ref _channelDepth, delta);
        }
    }

    public void DecreaseQueueDepth(int delta = 1)
    {
        if (delta > 0)
        {
            Interlocked.Add(ref _channelDepth, -delta);
        }
    }
}

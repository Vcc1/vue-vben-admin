using System.Threading;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoIngestionState
{
    long _queueRemaining;
    long _inProgress;
    long _completed;
    long _listPending;
    long _setPending;
    long _dedupCacheCount;
    long _dedupCacheHit;
    long _dedupCacheMiss;

    public long QueueRemaining => Interlocked.Read(ref _queueRemaining);
    public long InProgress => Interlocked.Read(ref _inProgress);
    public long Completed => Interlocked.Read(ref _completed);
    public long ListPending => Interlocked.Read(ref _listPending);
    public long SetPending => Interlocked.Read(ref _setPending);
    public long DedupCacheCount => Interlocked.Read(ref _dedupCacheCount);
    public long DedupCacheHit => Interlocked.Read(ref _dedupCacheHit);
    public long DedupCacheMiss => Interlocked.Read(ref _dedupCacheMiss);
    public double DedupCacheHitRate
    {
        get
        {
            var hit = DedupCacheHit;
            var miss = DedupCacheMiss;
            var total = hit + miss;
            return total <= 0 ? 0 : (double)hit / total;
        }
    }

    public void IncreaseQueueRemaining(int delta) => Add(ref _queueRemaining, delta);
    public void DecreaseQueueRemaining(int delta) => Add(ref _queueRemaining, -delta);
    public void IncreaseInProgress(int delta) => Add(ref _inProgress, delta);
    public void DecreaseInProgress(int delta) => Add(ref _inProgress, -delta);
    public void IncreaseCompleted(int delta) => Add(ref _completed, delta);
    public void SetListPending(long value) => Interlocked.Exchange(ref _listPending, Math.Max(0, value));
    public void SetSetPending(long value) => Interlocked.Exchange(ref _setPending, Math.Max(0, value));
    public void SetDedupCacheCount(long value) => Interlocked.Exchange(ref _dedupCacheCount, Math.Max(0, value));
    public void IncreaseDedupCacheHit(int delta = 1) => Add(ref _dedupCacheHit, delta);
    public void IncreaseDedupCacheMiss(int delta = 1) => Add(ref _dedupCacheMiss, delta);

    private static void Add(ref long field, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        Interlocked.Add(ref field, delta);
        if (Interlocked.Read(ref field) < 0)
        {
            Interlocked.Exchange(ref field, 0);
        }
    }
}

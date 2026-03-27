using System.Threading;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomAppState
{
    private long _queueRemaining;
    private long _inProgress;
    private long _completed;

    public long QueueRemaining => Interlocked.Read(ref _queueRemaining);

    public long InProgress => Interlocked.Read(ref _inProgress);

    public long Completed => Interlocked.Read(ref _completed);

    public bool HasPendingWork => QueueRemaining > 0 || InProgress > 0;

    public void IncreaseQueueRemaining() => Interlocked.Increment(ref _queueRemaining);

    public void DecreaseQueueRemaining() => Interlocked.Decrement(ref _queueRemaining);

    public void IncreaseInProgress() => Interlocked.Increment(ref _inProgress);

    public void DecreaseInProgress() => Interlocked.Decrement(ref _inProgress);

    public void IncreaseCompleted() => Interlocked.Increment(ref _completed);
}
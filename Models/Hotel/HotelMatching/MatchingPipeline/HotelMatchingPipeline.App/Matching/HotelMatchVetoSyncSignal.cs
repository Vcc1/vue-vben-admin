namespace HotelMatchingPipeline.App;

public sealed class HotelMatchVetoSyncSignal
{
    readonly SemaphoreSlim _signal = new(0, 1);

    public void Pulse()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => _signal.WaitAsync(timeout, cancellationToken);
}

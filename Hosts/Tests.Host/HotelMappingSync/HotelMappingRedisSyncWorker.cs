using System.Threading.Channels;

namespace Tests.Host.HotelMappingSync;

public sealed class HotelMappingRedisSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<HotelMappingRedisSyncWorker> logger) : BackgroundService
{
    readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });
    readonly Lock _statusLock = new();
    HotelMappingRedisSyncStatus _status = HotelMappingRedisSyncStatus.Idle;

    public HotelMappingRedisSyncStatus GetStatus()
    {
        lock (_statusLock)
        {
            return _status;
        }
    }

    public bool TryStart(out HotelMappingRedisSyncStatus status)
    {
        lock (_statusLock)
        {
            if (_status.State == "running")
            {
                status = _status;
                return false;
            }

            var startedAt = DateTimeOffset.Now;
            _status = new HotelMappingRedisSyncStatus(
                "running", "starting", startedAt, null, 0, 0, 0, 0, 0, 0, 0, 0, null);
            if (!_requests.Writer.TryWrite(true))
            {
                _status = _status with
                {
                    State = "failed",
                    CompletedAt = DateTimeOffset.Now,
                    Error = "Unable to enqueue the Redis synchronization request."
                };
                status = _status;
                return false;
            }

            status = _status;
            return true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in _requests.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<HotelMappingSyncRunner>();
                var progress = await runner.RunAsync(UpdateProgress, stoppingToken);
                SetStatus(current => current with
                {
                    State = progress.Warning is null ? "succeeded" : "partial",
                    Phase = "completed",
                    CompletedAt = DateTimeOffset.Now,
                    SourceCount = progress.SourceCount,
                    RedisCount = progress.RedisCount,
                    AddedCount = progress.AddedCount,
                    UpdatedCount = progress.UpdatedCount,
                    DeletedCount = progress.DeletedCount,
                    AppliedCount = progress.AppliedCount,
                    QueuedCount = progress.QueuedCount,
                    PendingCount = progress.PendingCount,
                    Error = null,
                    Warning = progress.Warning
                });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Hotel mapping Redis synchronization failed.");
                SetStatus(current => current with
                {
                    State = "failed",
                    CompletedAt = DateTimeOffset.Now,
                    Error = ex.Message,
                    Warning = null
                });
            }
        }
    }

    void UpdateProgress(HotelMappingSyncProgress progress)
    {
        SetStatus(current => current with
        {
            Phase = progress.Phase,
            SourceCount = progress.SourceCount,
            RedisCount = progress.RedisCount,
            AddedCount = progress.AddedCount,
            UpdatedCount = progress.UpdatedCount,
            DeletedCount = progress.DeletedCount,
            AppliedCount = progress.AppliedCount,
            QueuedCount = progress.QueuedCount,
            PendingCount = progress.PendingCount,
            Warning = progress.Warning
        });
    }

    void SetStatus(Func<HotelMappingRedisSyncStatus, HotelMappingRedisSyncStatus> update)
    {
        lock (_statusLock)
        {
            _status = update(_status);
        }
    }
}

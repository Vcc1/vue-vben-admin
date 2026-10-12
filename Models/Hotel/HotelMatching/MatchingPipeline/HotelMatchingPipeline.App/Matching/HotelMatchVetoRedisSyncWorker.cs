using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.App;

public sealed class HotelMatchVetoRedisSyncWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IHotelMatchVetoRedisSynchronizer _redisSynchronizer;
    readonly IHotelMatchVetoNotifier _notifier;
    readonly HotelMatchVetoSyncSignal _signal;
    readonly IOptionsMonitor<HotelMatchVetoOptions> _options;
    readonly ILogger<HotelMatchVetoRedisSyncWorker> _logger;

    public HotelMatchVetoRedisSyncWorker(
        IServiceScopeFactory scopeFactory,
        IHotelMatchVetoRedisSynchronizer redisSynchronizer,
        IHotelMatchVetoNotifier notifier,
        HotelMatchVetoSyncSignal signal,
        IOptionsMonitor<HotelMatchVetoOptions> options,
        ILogger<HotelMatchVetoRedisSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _redisSynchronizer = redisSynchronizer;
        _notifier = notifier;
        _signal = signal;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var useFastRetry = false;
            try
            {
                useFastRetry = await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotel match veto pending synchronization scan failed.");
            }
            var options = _options.CurrentValue;
            var delaySeconds = useFastRetry
                ? options.FastRetryDelaySeconds
                : options.RetryDelaySeconds;
            var delay = TimeSpan.FromSeconds(Math.Max(1, delaySeconds));
            try
            {
                await _signal.WaitAsync(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    async Task<bool> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IHotelMatchVetoRepository>();
        var batchSize = Math.Clamp(_options.CurrentValue.SyncBatchSize, 1, 1000);
        var pending = await repository.LoadPendingSyncAsync(batchSize, cancellationToken);
        var useFastRetry = false;
        foreach (var veto in pending)
        {
            if (veto.RedisSyncedAt.HasValue)
            {
                var alerted = veto;
                if (alerted.FirstRedisSyncFailureAt.HasValue && !alerted.FailureAlertSentAt.HasValue)
                {
                    alerted = await TrySendFailureAsync(repository, alerted, cancellationToken);
                }
                await TrySendRecoveryAsync(repository, alerted, cancellationToken);
                continue;
            }

            try
            {
                await _redisSynchronizer.RemoveAsync(veto, cancellationToken);
                var synced = await repository.MarkRedisSyncedAsync(
                    veto.Id,
                    DateTimeOffset.Now,
                    cancellationToken);
                await TrySendRecoveryAsync(repository, synced, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Hotel match veto Redis synchronization failed. VetoId={VetoId}, SupId={SupId}, SHotelCode={SHotelCode}, MTHotelCode={MTHotelCode}",
                    veto.Id,
                    veto.SupId,
                    veto.SHotelCode,
                    veto.MTHotelCode);
                var failed = await repository.RecordRedisFailureAsync(
                    veto.Id,
                    ex.ToString(),
                    DateTimeOffset.Now,
                    cancellationToken);
                if (!failed.FailureAlertSentAt.HasValue)
                {
                    await TrySendFailureAsync(repository, failed, cancellationToken);
                }
                if (failed.RedisSyncAttemptCount <= _options.CurrentValue.FastRetryFailureCount)
                {
                    useFastRetry = true;
                }
            }
        }
        return useFastRetry;
    }

    async Task<Models.HotelMatchVetoSyncItem> TrySendFailureAsync(
        IHotelMatchVetoRepository repository,
        Models.HotelMatchVetoSyncItem veto,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.SendFailureAsync(veto, cancellationToken);
            var sentAt = DateTimeOffset.Now;
            await repository.MarkFailureAlertSentAsync(veto.Id, sentAt, cancellationToken);
            return veto with { FailureAlertSentAt = sentAt };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send hotel match veto failure alert. VetoId={VetoId}", veto.Id);
            return veto;
        }
    }

    async Task TrySendRecoveryAsync(
        IHotelMatchVetoRepository repository,
        Models.HotelMatchVetoSyncItem veto,
        CancellationToken cancellationToken)
    {
        if (!veto.FailureAlertSentAt.HasValue || veto.RecoveryAlertSentAt.HasValue)
        {
            return;
        }

        try
        {
            await _notifier.SendRecoveryAsync(veto, cancellationToken);
            await repository.MarkRecoveryAlertSentAsync(veto.Id, DateTimeOffset.Now, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send hotel match veto recovery alert. VetoId={VetoId}", veto.Id);
        }
    }
}

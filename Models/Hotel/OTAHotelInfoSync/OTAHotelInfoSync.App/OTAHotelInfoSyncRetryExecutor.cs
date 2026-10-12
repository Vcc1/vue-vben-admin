using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTAHotelInfoSync.Contracts;

namespace OTAHotelInfoSync;

public sealed class OTAHotelInfoSyncRetryExecutor
{
    readonly IOTAHotelInfoSyncNotifier _notifier;
    readonly IOptionsMonitor<OTAHotelInfoSyncOptions> _options;
    readonly ILogger<OTAHotelInfoSyncRetryExecutor> _logger;

    public OTAHotelInfoSyncRetryExecutor(
        IOTAHotelInfoSyncNotifier notifier,
        IOptionsMonitor<OTAHotelInfoSyncOptions> options,
        ILogger<OTAHotelInfoSyncRetryExecutor> logger)
    {
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    public Task ExecuteAsync(
        string failureType,
        string operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync<object?>(
            failureType,
            operation,
            async token =>
            {
                await action(token);
                return null;
            },
            cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        string failureType,
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var consecutiveFailureCount = 0;
        DateTimeOffset? firstFailedAt = null;
        var alertSent = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await action(cancellationToken);
                if (alertSent && firstFailedAt.HasValue)
                {
                    await TrySendRecoveryAsync(
                        new OTAHotelInfoSyncRecoveryNotification(
                            failureType,
                            operation,
                            consecutiveFailureCount,
                            firstFailedAt.Value,
                            OTAHotelInfoSyncTime.Now),
                        cancellationToken);
                }

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                consecutiveFailureCount++;
                var failedAt = OTAHotelInfoSyncTime.Now;
                firstFailedAt ??= failedAt;
                var options = _options.CurrentValue;

                _logger.LogError(
                    ex,
                    "OTA hotel information {FailureType} failed. Operation={Operation}, ConsecutiveFailures={ConsecutiveFailures}, RetryDelaySeconds={RetryDelaySeconds}",
                    failureType,
                    operation,
                    consecutiveFailureCount,
                    options.RetryDelaySeconds);

                if (!alertSent && consecutiveFailureCount >= Math.Max(1, options.AlertAfterFailures))
                {
                    alertSent = await TrySendFailureAsync(
                        new OTAHotelInfoSyncFailureNotification(
                            failureType,
                            operation,
                            consecutiveFailureCount,
                            firstFailedAt.Value,
                            failedAt,
                            ex.Message),
                        cancellationToken);
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, options.RetryDelaySeconds)),
                    cancellationToken);
            }
        }
    }

    async Task<bool> TrySendFailureAsync(
        OTAHotelInfoSyncFailureNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.SendFailureAsync(notification, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send OTA hotel information failure alert. FailureType={FailureType}, Operation={Operation}",
                notification.FailureType,
                notification.Operation);
            return false;
        }
    }

    async Task TrySendRecoveryAsync(
        OTAHotelInfoSyncRecoveryNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.SendRecoveryAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send OTA hotel information recovery alert. FailureType={FailureType}, Operation={Operation}",
                notification.FailureType,
                notification.Operation);
        }
    }
}

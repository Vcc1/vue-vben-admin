namespace OTAHotelInfoSync.Contracts;

public interface IOTAHotelInfoSyncNotifier
{
    Task SendFailureAsync(
        OTAHotelInfoSyncFailureNotification notification,
        CancellationToken cancellationToken = default);

    Task SendRecoveryAsync(
        OTAHotelInfoSyncRecoveryNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record OTAHotelInfoSyncFailureNotification(
    string FailureType,
    string Operation,
    int ConsecutiveFailureCount,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset FailedAt,
    string ErrorMessage);

public sealed record OTAHotelInfoSyncRecoveryNotification(
    string FailureType,
    string Operation,
    int ConsecutiveFailureCount,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset RecoveredAt);

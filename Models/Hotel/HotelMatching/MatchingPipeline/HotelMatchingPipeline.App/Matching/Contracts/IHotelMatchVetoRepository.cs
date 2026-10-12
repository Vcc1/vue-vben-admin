using HotelMatchingPipeline.App.Models;

namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelMatchVetoRepository
{
    Task<HotelMatchVetoCreateResult> CreateAsync(
        HotelMatchVetoRequest request,
        CancellationToken cancellationToken);

    Task<HotelMatchVetoSyncItem?> GetAsync(long vetoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HotelMatchVetoSyncItem>> LoadPendingSyncAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task<HotelMatchVetoSyncItem> RecordRedisFailureAsync(
        long vetoId,
        string error,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken);

    Task MarkFailureAlertSentAsync(
        long vetoId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    Task<HotelMatchVetoSyncItem> MarkRedisSyncedAsync(
        long vetoId,
        DateTimeOffset syncedAt,
        CancellationToken cancellationToken);

    Task MarkRecoveryAlertSentAsync(
        long vetoId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    Task<bool> IsVetoedAsync(
        int supId,
        string supplierHotelCode,
        string mtHotelCode,
        CancellationToken cancellationToken);
}

public interface IHotelMatchVetoRedisSynchronizer
{
    Task RemoveAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken);
}

public interface IHotelMatchVetoNotifier
{
    Task SendFailureAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken);

    Task SendRecoveryAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken);
}

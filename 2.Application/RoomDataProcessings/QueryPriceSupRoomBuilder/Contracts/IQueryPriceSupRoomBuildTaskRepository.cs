using OTADomain.RoomDataProcessings;

namespace QueryPriceSupRoomBuilder;

public interface IQueryPriceSupRoomBuildTaskRepository
{
    Task<int> UpsertTriggeredAsync(IReadOnlyList<string> mtHotelCodes, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueryPriceSupRoomBuildTaskModel>> ClaimAsync(
        int batchSize,
        string targetSuppliers,
        CancellationToken cancellationToken);

    Task MarkCompletedAsync(int id, int queuedCount, CancellationToken cancellationToken);

    Task MarkNoHitAsync(int id, CancellationToken cancellationToken);

    Task MarkRetryOrFailedAsync(
        int id,
        int maxRetryCount,
        string error,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<QueryPriceSupRoomBuildTaskStatus, long>> CountByStatusAsync(CancellationToken cancellationToken);
}

namespace SupRoomInfoIngestion.Contracts;

public interface ISupRoomInfoSetReader
{
    Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(
        string setKeyPrefix,
        int fetchBatchSize,
        CancellationToken cancellationToken);

    Task<long> GetPendingCountAsync(string setKeyPrefix, CancellationToken cancellationToken);
}

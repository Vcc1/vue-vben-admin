namespace SupRoomInfoIngestion.Contracts;

public interface ISupRoomInfoListReader
{
    Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(int maxSize, CancellationToken cancellationToken);

    Task<long> GetPendingCountAsync(string listKey, CancellationToken cancellationToken);
}

namespace SupRoomInfoIngestion.Contracts;

public interface ISupRoomInfoSourceStep
{
    string SourceName { get; }

    Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(CancellationToken cancellationToken);

    Task<long> GetPendingCountAsync(CancellationToken cancellationToken);
}

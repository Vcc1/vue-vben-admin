namespace SupRoomInfoIngestion.Contracts;

public interface ISupRoomInfoBatchWriter
{
    Task<int> WriteAsync(IReadOnlyList<TempSupRoomInfo2> batch, CancellationToken cancellationToken);
}

using Infrastructure.Sql.RoomMapping.Entitys;

namespace OTARoomBuilder.Contracts;

public interface IOtaRoomCreateClient
{
    Task<IReadOnlyList<RoomCreateResult>> PostAsync(
        IReadOnlyList<OTARoomInfoPre> rooms,
        CancellationToken cancellationToken);
}

public sealed record RoomCreateResult(
    int SupplierRoomId,
    string MTHotelCode,
    bool Succeeded,
    int Attempts,
    string? Error);

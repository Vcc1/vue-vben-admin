using Infrastructure.Sql.RoomMapping.Entitys;

namespace OTARoomBuilder.Contracts;

public interface IOtaRoomCreateClient
{
    Task PostAsync(IReadOnlyList<OTARoomInfoPre> rooms, CancellationToken cancellationToken);
}

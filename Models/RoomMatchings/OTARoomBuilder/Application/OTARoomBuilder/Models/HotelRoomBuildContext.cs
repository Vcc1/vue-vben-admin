using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;

namespace OTARoomBuilder;

public sealed class HotelRoomBuildContext
{
    public required string MTHotelCode { get; init; }

    public List<SupRoomInfo> SupplierRooms { get; init; } = [];

    public List<OTARoomInfo> OtaRooms { get; init; } = [];

    public List<RoomMap> RoomMaps { get; init; } = [];
}

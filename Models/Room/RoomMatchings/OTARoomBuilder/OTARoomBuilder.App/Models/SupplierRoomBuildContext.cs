using Infrastructure.Sql.MengTuSup.Entitys;
using RoomMatch.Core.RoomAnalysis;

namespace OTARoomBuilder;

public sealed class SupplierRoomBuildContext
{
    public required SupRoomInfo SupplierRoom { get; init; }

    public RoomContext? RoomContext { get; init; }

    public string? MTHotelCode => SupplierRoom.MTHotelCode;

    public string? SHotelCode => SupplierRoom.SHotelCode;

    public string? SRoomCode => SupplierRoom.SRoomCode;
}

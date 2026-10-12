using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;
using RoomMatch.Core.RoomAnalysis;

namespace OTARoomBuilder;

public sealed class HotelRoomMatchResult
{
    public required string MTHotelCode { get; init; }

    public IReadOnlyList<SupplierRoomBuildCandidate> Candidates { get; init; } = [];

    public static HotelRoomMatchResult Empty(string mtHotelCode) => new()
    {
        MTHotelCode = mtHotelCode,
        Candidates = []
    };
}

public sealed class SupplierRoomBuildCandidate
{
    public required SupRoomInfo SupplierRoom { get; init; }

    public RoomContext? RoomContext { get; init; }

    public required OTARoomInfoPre DraftRoom { get; init; }
}

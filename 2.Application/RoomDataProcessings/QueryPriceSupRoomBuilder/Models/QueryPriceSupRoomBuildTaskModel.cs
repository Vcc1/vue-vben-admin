using OTADomain.RoomDataProcessings;

namespace QueryPriceSupRoomBuilder;

public sealed record QueryPriceSupRoomBuildTaskModel
{
    public int Id { get; init; }
    public string MTHotelCode { get; init; } = "";
    public QueryPriceSupRoomBuildTaskStatus Status { get; init; }
    public bool IsRoomBuilt { get; init; }
    public string? TargetSuppliers { get; init; }
    public int AttemptCount { get; init; }
}

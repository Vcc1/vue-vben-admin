namespace Infrastructure.Redis.SupRoomInfo.CachePriceDiscovery.Models;

public sealed class CachePriceDiscoveryRoomModel
{
    public required string MTHotelCode { get; init; }
    public required string SupHotelCode { get; init; }
    public required string RoomName { get; init; }
    public required string BedInfoKey { get; init; }
}

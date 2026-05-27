namespace QueryPriceSupRoomBuilder;

public sealed record QueryPriceRequest(
    IReadOnlyList<string> ListMTHotelCode,
    string SupId,
    int AdultNumber,
    int ChildNumber,
    DateTime CheckIn,
    DateTime CheckOut,
    int MTNationalityId = 74);

public sealed record QueryPriceResponse(int? Code, string? Msg, IReadOnlyList<QueryPriceRoom> ListPrice);

public sealed record QueryPriceRoom
{
    public string? SupId { get; init; }
    public string? MTHotelCode { get; init; }
    public string? SupplierName { get; init; }
    public string? SupRoomName { get; init; }
    public string? SupRoomEnName { get; init; }
    public string? SupRoomCode { get; init; }
    public string? SupHotelCode { get; init; }
    public string? SupBedInfo { get; init; }



    public string? BedInfoKey { get; init; }
    public int Person { get; init; }
    public string? CheckIn { get; init; }
    public string? CheckOut { get; init; }
    public decimal SupTotalPrice { get; init; }
    public decimal SupTotalPriceRMB { get; init; }
}

public sealed record SupRoomRedisMessage
{
    public string? RoomName { get; init; }
    public string? RoomEnName { get; init; }
    public string? HotelCode { get; init; }
    public string? MTHotelCode { get; init; }
    public string? RoomCode { get; init; }
    public string? BedType { get; init; }
    public string? BedInfo { get; init; }
    public string? BedInfoKey { get; init; }
    public int AcreageFrom { get; init; }
    public int AcreageTo { get; init; }
    public int SmokeType { get; init; }
    public int MaxAdt { get; init; }
    public int SupId { get; init; }
    public string? CreateTime { get; init; }
}

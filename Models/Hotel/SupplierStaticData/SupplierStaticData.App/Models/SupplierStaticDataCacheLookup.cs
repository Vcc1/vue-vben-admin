namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataCacheLookup
{
    public int SupId { get; init; }

    public string? MTHotelCode { get; init; }

    public string HotelCode { get; init; } = string.Empty;

    public string? RoomCode { get; init; }

    public SupplierStaticDataKind DataKind { get; init; }
}

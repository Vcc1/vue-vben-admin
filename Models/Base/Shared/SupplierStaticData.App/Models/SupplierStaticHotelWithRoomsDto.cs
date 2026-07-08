namespace SupplierStaticDataApp;

public sealed class SupplierStaticHotelWithRoomsDto
{
    public SupplierStaticHotelDto Hotel { get; set; } = new();

    public IReadOnlyList<SupplierStaticRoomDto> Rooms { get; set; } = [];
}

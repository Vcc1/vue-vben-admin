namespace SupplierStaticDataApp;

public sealed class SupplierStaticRoomDto
{
    public string RoomCode { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? NameEn { get; set; }

    public int? MaxOccupancy { get; set; }

    public Dictionary<string, string?> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

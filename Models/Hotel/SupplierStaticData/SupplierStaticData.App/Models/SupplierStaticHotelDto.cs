namespace SupplierStaticDataApp;

public sealed class SupplierStaticHotelDto
{
    public int SupId { get; set; }

    public string HotelCode { get; set; } = string.Empty;

    public string? Name { get; set; }

    public SupplierStaticGeoAdministrativeAreaDto? RawAdministrativeArea { get; set; }

    public Dictionary<string, string?> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

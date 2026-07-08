namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataRawInfo
{
    public string Source { get; set; } = string.Empty;

    public string? RequestUrl { get; set; }

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public Dictionary<string, SupplierStaticDataRawInfo>? Items { get; set; }
}

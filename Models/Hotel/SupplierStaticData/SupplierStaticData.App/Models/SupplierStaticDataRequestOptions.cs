namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataRequestOptions
{
    public bool ForceRefresh { get; set; }

    public bool IncludeRawInfo { get; set; }

    public string? Language { get; set; }

    public string? MTHotelCode { get; set; }

    public Dictionary<string, string>? MTHotelCodesBySupplierHotelCode { get; set; }

    public string GetLanguageOrDefault()
    {
        return string.IsNullOrWhiteSpace(Language) ? "en" : Language.Trim();
    }

    public string? GetMTHotelCode(string supplierHotelCode)
    {
        if (!string.IsNullOrWhiteSpace(MTHotelCode))
        {
            return MTHotelCode.Trim();
        }

        if (string.IsNullOrWhiteSpace(supplierHotelCode) || MTHotelCodesBySupplierHotelCode is null)
        {
            return null;
        }

        return MTHotelCodesBySupplierHotelCode.TryGetValue(supplierHotelCode.Trim(), out var mtHotelCode) &&
            !string.IsNullOrWhiteSpace(mtHotelCode)
            ? mtHotelCode.Trim()
            : null;
    }
}

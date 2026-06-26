namespace SupplierStaticDataApp;

public sealed class SupplierStaticDataRequestOptions
{
    public bool ForceRefresh { get; set; }

    public bool IncludeRawInfo { get; set; }

    public string? Language { get; set; }

    public string GetLanguageOrDefault()
    {
        return string.IsNullOrWhiteSpace(Language) ? "en" : Language.Trim();
    }
}

using System.Globalization;

namespace HotelMatchingPipeline.Domain.Hotels;

/// <summary>Normalizes ISO 3166-1 alpha-2 and alpha-3 country codes to alpha-2.</summary>
public static class CountryCodeNormalizer
{
    static readonly IReadOnlyDictionary<string, string> CodeToAlpha2 = BuildCodeMap();

    public static string? ToAlpha2(string? countryCode)
    {
        var value = countryCode?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(value) ? null : CodeToAlpha2.GetValueOrDefault(value);
    }

    public static bool IsAlphaCode(string? countryCode)
    {
        var value = countryCode?.Trim();
        return value is { Length: 2 or 3 }
            && value.All(static character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    public static bool AreEquivalentIsoCodes(string? left, string? right)
    {
        if (!IsAlphaCode(left) || !IsAlphaCode(right))
        {
            return false;
        }

        var leftAlpha2 = ToAlpha2(left);
        var rightAlpha2 = ToAlpha2(right);
        return leftAlpha2 is not null
            && rightAlpha2 is not null
            && string.Equals(leftAlpha2, rightAlpha2, StringComparison.Ordinal);
    }

    public static bool AreEquivalent(string? left, string? right)
    {
        var leftAlpha2 = ToAlpha2(left);
        var rightAlpha2 = ToAlpha2(right);
        if (leftAlpha2 is not null && rightAlpha2 is not null)
        {
            return string.Equals(leftAlpha2, rightAlpha2, StringComparison.Ordinal);
        }

        // Preserve comparisons for provider-specific or test-only region codes.
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyDictionary<string, string> BuildCodeMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Specific cultures already represent the valid ISO regions. Building from
        // them avoids probing all 676 alpha-2 combinations and using caught
        // ArgumentExceptions as validation for invalid region codes.
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            var region = new RegionInfo(culture.Name);
            var alpha2 = region.TwoLetterISORegionName.ToUpperInvariant();
            var alpha3 = region.ThreeLetterISORegionName.ToUpperInvariant();
            if (alpha2.Length == 2
                && alpha2.All(static character => character is >= 'A' and <= 'Z')
                && alpha3.Length == 3
                && alpha3.All(static character => character is >= 'A' and <= 'Z')
                && alpha2 is not "CS" and not "IV")
            {
                result[alpha2] = alpha2;
                result[alpha3] = alpha2;
            }
        }

        // ISO territories that are neutral in the installed .NET globalization data.
        Add(result, "AQ", "ATA"); // Antarctica
        Add(result, "BV", "BVT"); // Bouvet Island
        Add(result, "EH", "ESH"); // Western Sahara
        Add(result, "GS", "SGS"); // South Georgia and South Sandwich Islands
        Add(result, "HM", "HMD"); // Heard Island and McDonald Islands
        Add(result, "TF", "ATF"); // French Southern Territories

        // Common supplier aliases; these are not additional ISO identities.
        result["UK"] = "GB";
        result["EL"] = "GR";
        result["XKS"] = "XK";
        return result;
    }

    static void Add(IDictionary<string, string> map, string alpha2, string alpha3)
    {
        map[alpha2] = alpha2;
        map[alpha3] = alpha2;
    }
}

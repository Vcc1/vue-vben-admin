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
        for (var first = 'A'; first <= 'Z'; first++)
        {
            for (var second = 'A'; second <= 'Z'; second++)
            {
                var alpha2 = $"{first}{second}";
                try
                {
                    var region = new RegionInfo(alpha2);
                    var alpha3 = region.ThreeLetterISORegionName.ToUpperInvariant();
                    if (alpha3.Length == 3
                        && alpha3.All(static character => character is >= 'A' and <= 'Z')
                        && alpha2 is not "CS" and not "IV")
                    {
                        result[alpha2] = alpha2;
                        result[alpha3] = alpha2;
                    }
                }
                catch (ArgumentException)
                {
                    // Some ISO territories have no specific .NET culture and are added below.
                }
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

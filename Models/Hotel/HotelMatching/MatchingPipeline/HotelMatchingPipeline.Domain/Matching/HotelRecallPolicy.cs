namespace HotelMatchingPipeline.Domain.Hotels;

public readonly record struct HotelRecallParameters(
    int RadiusMeters,
    int? MaxCandidates);

public static class HotelRecallPolicy
{
    public const int NonChinaRadiusMeters = 100;

    public static bool IsMatchingEnabled(string? regionLevel1Code)
        => !string.IsNullOrWhiteSpace(regionLevel1Code)
            && !IsGreaterChinaCode(
                CountryCodeNormalizer.ToAlpha2(regionLevel1Code));

    public static HotelRecallParameters Resolve(
        string? regionLevel1Code,
        string? countryCode,
        int configuredRadiusMeters,
        int configuredMaxCandidates)
    {
        _ = countryCode;
        var normalizedRegionLevel1Code = CountryCodeNormalizer.ToAlpha2(regionLevel1Code);
        return IsGreaterChinaCode(normalizedRegionLevel1Code)
            ? new(configuredRadiusMeters, configuredMaxCandidates)
            : new(NonChinaRadiusMeters, null);
    }

    static bool IsGreaterChinaCode(string? normalizedCountryCode)
        => normalizedCountryCode is "CN" or "TW" or "HK" or "MO";
}

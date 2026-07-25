namespace HotelMatchingPipeline.Domain.Hotels;

public readonly record struct HotelRecallParameters(
    int RadiusMeters,
    int? MaxCandidates);

public static class HotelRecallPolicy
{
    public const int NonChinaRadiusMeters = 300;

    public static HotelRecallParameters Resolve(
        string? regionLevel1Code,
        string? countryCode,
        int configuredRadiusMeters,
        int configuredMaxCandidates)
    {
        var normalizedCountryCode =
            CountryCodeNormalizer.ToAlpha2(regionLevel1Code)
            ?? CountryCodeNormalizer.ToAlpha2(countryCode);
        return normalizedCountryCode is null or "CN"
            ? new(configuredRadiusMeters, configuredMaxCandidates)
            : new(NonChinaRadiusMeters, null);
    }
}

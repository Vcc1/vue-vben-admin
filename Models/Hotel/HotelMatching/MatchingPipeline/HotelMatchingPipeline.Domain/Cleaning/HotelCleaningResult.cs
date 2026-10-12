using HotelMatchingPipeline.Domain.HotelNameMatching;
using PhoneNumbers;

namespace HotelMatchingPipeline.Domain.Hotels;

public sealed record HotelCleaningResult(
    RawHotelData Raw,
    CleanedHotel Cleaned,
    HotelCleanStatus CleanStatus,
    HotelQualityAssessment Quality,
    string SourceDataHash,
    string NormalizedDataHash,
    int CleanVersion,
    int RegionBoundaryVersion,
    string? Error)
{
    public bool IsReady => CleanStatus == HotelCleanStatus.Ready;

    public bool IsMatchable => IsReady && Quality.IsValid;
}

public sealed class HotelCleaner
{
    static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    readonly IHotelRegionResolver _regionResolver;
    readonly IHotelPhoneNormalizer _phoneNormalizer;
    readonly HotelNameMatcher _nameMatcher;

    public HotelCleaner(
        IHotelRegionResolver regionResolver,
        IHotelPhoneNormalizer? phoneNormalizer = null,
        HotelNameMatcher? nameMatcher = null)
    {
        _regionResolver = regionResolver ?? throw new ArgumentNullException(nameof(regionResolver));
        _phoneNormalizer = phoneNormalizer ?? new LibPhoneNumberNormalizer();
        _nameMatcher = nameMatcher ?? new HotelNameMatcher();
    }

    public HotelCleaningResult Clean(
        RawHotelData raw,
        int cleanVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(raw);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceHash = string.IsNullOrWhiteSpace(raw.SourceDataHash)
            ? HotelDataHasher.HashRaw(raw)
            : raw.SourceDataHash;
        var flags = HotelDataQualityFlags.None;
        var details = new Dictionary<string, string?>(StringComparer.Ordinal);
        var coordinate = HotelCoordinateNormalizer.Normalize(raw.Longitude, raw.Latitude, raw.GpsType);
        var region = coordinate.IsValid
            ? _regionResolver.Resolve(
                coordinate.Longitude!.Value,
                coordinate.Latitude!.Value,
                raw.CountryCode,
                cancellationToken)
            : RegionResolution.NotFound();

        if (CountryCodeNormalizer.IsAlphaCode(raw.CountryCode)
            && raw.CountryCode is { } databaseCountryCode
            && !RegionMatchesCountry(region, databaseCountryCode))
        {
            var swappedCoordinate = HotelCoordinateNormalizer.Normalize(raw.Latitude, raw.Longitude, raw.GpsType);
            if (swappedCoordinate.IsValid)
            {
                var swappedRegion = _regionResolver.Resolve(
                    swappedCoordinate.Longitude!.Value,
                    swappedCoordinate.Latitude!.Value,
                    raw.CountryCode,
                    cancellationToken);
                if (RegionMatchesCountry(swappedRegion, databaseCountryCode))
                {
                    coordinate = swappedCoordinate;
                    region = swappedRegion;
                    details["coordinate_order"] = "swapped_to_match_country";
                }
            }
        }

        if (!coordinate.IsValid)
        {
            flags |= coordinate.Error == "unknown_coordinate_type"
                ? HotelDataQualityFlags.CoordinateTypeUnknown
                : HotelDataQualityFlags.CoordinateInvalid;
            details["coordinate"] = coordinate.Error;
        }

        if (region.Status == RegionResolveStatus.NotFound)
        {
            flags |= HotelDataQualityFlags.RegionNotFound;
            details["region"] = "not_found";
        }
        else if (region.Status == RegionResolveStatus.BoundaryAmbiguous)
        {
            flags |= HotelDataQualityFlags.RegionBoundaryAmbiguous;
            details["region"] = "boundary_ambiguous";
        }

        if (region.Status == RegionResolveStatus.Resolved && string.IsNullOrWhiteSpace(region.Level1Code))
        {
            flags |= HotelDataQualityFlags.RegionCodeMissing;
            details["region_code"] = "level1_missing";
        }

        if (string.IsNullOrWhiteSpace(raw.HotelName) && string.IsNullOrWhiteSpace(raw.HotelNameEn))
        {
            flags |= HotelDataQualityFlags.HotelNameMissing;
            details["name"] = "missing";
        }

        var phoneResult = _phoneNormalizer.Normalize(raw.Telephone, region, raw.CountryCode);
        var invalidPhones = phoneResult.Where(x => !x.IsValid).ToArray();
        if (invalidPhones.Length > 0)
        {
            flags |= HotelDataQualityFlags.PhoneInvalid;
            details["phone"] = string.Join(',', invalidPhones.Select(x => x.Error).Where(x => x is not null));
        }

        var phoneCountryCodes = phoneResult.Where(x => x.IsValid && x.CountryCallingCode is not null)
            .Select(x => x.CountryCallingCode!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (phoneCountryCodes.Length > 1)
        {
            flags |= HotelDataQualityFlags.MultiplePhoneConflict;
            details["phone_country_codes"] = string.Join(',', phoneCountryCodes);
        }

        var expectedCallingCodes = new[] { raw.CountryCode, region.Level1Code }
            .Where(code => TryGetCallingCode(code, out _))
            .Select(code => GetCallingCode(code)!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (phoneCountryCodes.Length > 0
            && expectedCallingCodes.Length > 0
            && phoneCountryCodes.Any(code => !expectedCallingCodes.Contains(code, StringComparer.Ordinal)))
        {
            flags |= HotelDataQualityFlags.PhoneCountryConflict;
            details["phone_country_conflict"] = string.Join(',', expectedCallingCodes);
        }

        if (!string.IsNullOrWhiteSpace(region.TelephoneAreaCode)
            && phoneResult.Any(x => x.IsValid && x.AreaCode is not null && x.AreaCode != region.TelephoneAreaCode))
        {
            flags |= HotelDataQualityFlags.PhoneAreaConflict;
            details["phone_area_conflict"] = region.TelephoneAreaCode;
        }

        var hotelName = string.IsNullOrWhiteSpace(raw.HotelName) ? raw.HotelNameEn ?? string.Empty : raw.HotelName!;
        var cleaned = new CleanedHotel(
            raw.Type,
            raw.HotelCode,
            HotelNameMatcher.Normalize(hotelName),
            raw.SupplierId,
            raw.MtHotelCode,
            raw.CountryCode,
            raw.CountryName,
            raw.CityCode,
            raw.CityName,
            HotelNameMatcher.Normalize(raw.HotelNameEn),
            raw.Address,
            raw.PostCode,
            raw.Telephone,
            raw.Fax,
            raw.Email,
            raw.StarLevel,
            (int?)coordinate.Type,
            coordinate.Longitude,
            coordinate.Latitude)
        {
            RegionLevel1Code = region.Level1Code,
            RegionLevel1Name = region.Level1Name,
            RegionLevel2Code = region.Level2Code,
            RegionLevel2Name = region.Level2Name,
            RegionLevel3Code = region.Level3Code,
            RegionLevel3Name = region.Level3Name,
            RegionResolveStatus = region.Status,
            NormalizedPhones = phoneResult.Where(x => x.IsValid && x.E164 is not null).Select(x => x.E164!).Distinct(StringComparer.Ordinal).ToArray(),
            NormalizedPhoneDetails = phoneResult,
            DataQualityFlags = flags,
            DataQualityStatus = flags == HotelDataQualityFlags.None
                ? HotelDataQualityStatus.Valid
                : HotelDataQualityStatus.Problem
        };

        var quality = new HotelQualityAssessment(cleaned.DataQualityStatus, flags, details);
        return new HotelCleaningResult(
            raw,
            cleaned,
            HotelCleanStatus.Ready,
            quality,
            sourceHash,
            HotelDataHasher.HashCleaned(cleaned),
            cleanVersion,
            region.BoundaryVersion,
            null);
    }

    static bool TryGetCallingCode(string? countryCode, out string callingCode)
    {
        var alpha2 = CountryCodeNormalizer.ToAlpha2(countryCode);
        var code = alpha2 is null ? 0 : PhoneUtil.GetCountryCodeForRegion(alpha2);
        callingCode = code > 0
            ? code.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        return code > 0;
    }

    static string? GetCallingCode(string? countryCode)
        => TryGetCallingCode(countryCode, out var callingCode) ? callingCode : null;

    static bool RegionMatchesCountry(RegionResolution region, string countryCode)
        => region.IsResolved && CountryCodeNormalizer.AreEquivalentIsoCodes(region.Level1Code, countryCode);
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PhoneNumbers;

namespace HotelMatchingPipeline.Domain.Hotels;

public sealed record NormalizedPhone(
    string Raw,
    string? E164,
    string? NationalNumber,
    string? CountryCallingCode,
    string? AreaCode,
    string? Extension,
    bool IsValid,
    string? Error,
    string? RegionCode = null,
    string? NumberType = null);

public interface IHotelPhoneNormalizer
{
    IReadOnlyList<NormalizedPhone> Normalize(
        string? raw,
        RegionResolution region,
        string? countryCode);
}

/// <summary>Google libphonenumber metadata through the libphonenumber-csharp package.</summary>
public sealed class LibPhoneNumberNormalizer : IHotelPhoneNormalizer
{
    static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();
    static readonly Regex ExtensionPattern = new(
        @"(?:ext\.?|extension|x|#|转|分机)\s*[:：]?\s*(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    static readonly Regex PhoneSeparatorPattern = new(
        @"[;,/|，；、\r\n]+|(?<=\d)\s*(?=\+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public IReadOnlyList<NormalizedPhone> Normalize(string? raw, RegionResolution region, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var defaultRegion = ResolveRegion(countryCode)
            ?? (region.IsResolved ? ResolveRegion(region.Level1Code) : null);
        var normalizedRaw = raw.Normalize(NormalizationForm.FormKC);
        var parts = PhoneSeparatorPattern.Split(normalizedRaw)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim());
        return parts.Select(part => NormalizeOne(part, defaultRegion)).ToArray();
    }

    static NormalizedPhone NormalizeOne(string raw, string? defaultRegion)
    {
        var extensionMatch = ExtensionPattern.Match(raw);
        var extension = extensionMatch.Success ? extensionMatch.Groups[1].Value : null;
        var numberText = extensionMatch.Success ? raw[..extensionMatch.Index].Trim() : raw;
        var prepared = PrepareNumber(numberText);
        var digitCount = prepared.Count(char.IsDigit);
        if (digitCount < 2 || !PhoneNumberUtil.IsViablePhoneNumber(prepared))
        {
            return new NormalizedPhone(raw, null, null, null, null, extension, false, "not_viable_number");
        }

        if (digitCount > 15)
        {
            return new NormalizedPhone(raw, null, null, null, null, extension, false, "too_long");
        }

        var hasExplicitCountryCode = prepared.StartsWith('+');
        if (!hasExplicitCountryCode && defaultRegion is null)
        {
            return new NormalizedPhone(raw, null, null, null, null, extension, false, "missing_default_region");
        }

        if (hasExplicitCountryCode && !HasKnownCountryCallingCode(prepared))
        {
            return new NormalizedPhone(raw, null, null, null, null, extension, false, "invalid_country_code");
        }

        NormalizedPhone? invalid = null;
        NumberParseException? parseException = null;
        foreach (var parseText in GetParseCandidates(prepared, defaultRegion))
        {
            try
            {
                var parsed = PhoneUtil.Parse(parseText, defaultRegion ?? "ZZ");
                var type = PhoneUtil.GetNumberType(parsed);
                if (!PhoneUtil.IsValidNumber(parsed))
                {
                    invalid = Invalid(raw, extension, "invalid_number", parsed);
                    continue;
                }

                var national = PhoneUtil.GetNationalSignificantNumber(parsed);
                var regionCode = PhoneUtil.GetRegionCodeForNumber(parsed);
                return new NormalizedPhone(
                    raw,
                    PhoneUtil.Format(parsed, PhoneNumberFormat.E164),
                    national,
                    parsed.CountryCode.ToString(CultureInfo.InvariantCulture),
                    GetAreaCode(parsed, national, type),
                    extension,
                    true,
                    null,
                    regionCode,
                    type.ToString());
            }
            catch (NumberParseException ex)
            {
                parseException = ex;
            }
        }

        return invalid ?? new NormalizedPhone(
            raw,
            null,
            null,
            null,
            null,
            extension,
            false,
            parseException?.ErrorType.ToString() ?? "invalid_number");
    }

    static IEnumerable<string> GetParseCandidates(string prepared, string? defaultRegion)
    {
        if (prepared.StartsWith('+') || defaultRegion is null)
        {
            yield return prepared;
            yield break;
        }

        var callingCode = PhoneUtil.GetCountryCodeForRegion(defaultRegion).ToString(CultureInfo.InvariantCulture);
        if (prepared.StartsWith(callingCode, StringComparison.Ordinal)
            && prepared.Length > callingCode.Length + 2)
        {
            // Some suppliers omit '+' from an otherwise explicit international number.
            yield return $"+{prepared}";
        }

        yield return prepared;
    }

    static string PrepareNumber(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        var possibleNumber = PhoneNumberUtil.ExtractPossibleNumber(normalized);
        var hasLeadingPlus = possibleNumber.TrimStart().StartsWith('+');
        var digits = PhoneNumberUtil.NormalizeDigitsOnly(
            PhoneNumberUtil.ConvertAlphaCharactersInNumber(possibleNumber));
        if (!hasLeadingPlus && digits.StartsWith("00", StringComparison.Ordinal) && digits.Length > 2)
        {
            hasLeadingPlus = true;
            digits = digits[2..];
        }

        return hasLeadingPlus ? $"+{digits}" : digits;
    }

    static bool HasKnownCountryCallingCode(string prepared)
    {
        var digits = prepared.AsSpan(1);
        for (var length = 1; length <= Math.Min(3, digits.Length); length++)
        {
            if (!int.TryParse(digits[..length], NumberStyles.None, CultureInfo.InvariantCulture, out var callingCode))
            {
                return false;
            }

            if (!string.Equals(PhoneUtil.GetRegionCodeForCountryCode(callingCode), "ZZ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    static NormalizedPhone Invalid(string raw, string? extension, string error, PhoneNumber parsed)
    {
        var national = PhoneUtil.GetNationalSignificantNumber(parsed);
        var type = PhoneUtil.GetNumberType(parsed);
        return new NormalizedPhone(
            raw,
            null,
            national,
            parsed.CountryCode == 0 ? null : parsed.CountryCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            GetAreaCode(parsed, national, type),
            extension,
            false,
            error,
            PhoneUtil.GetRegionCodeForNumber(parsed),
            type.ToString());
    }

    static string? GetAreaCode(PhoneNumber parsed, string national, PhoneNumberType type)
    {
        if (type == PhoneNumberType.MOBILE) return null;
        var length = PhoneUtil.GetLengthOfGeographicalAreaCode(parsed);
        return length > 0 && national.Length >= length ? national[..length] : null;
    }

    static string? ResolveRegion(string? countryCode)
    {
        var alpha2 = CountryCodeNormalizer.ToAlpha2(countryCode);
        return alpha2 is not null && PhoneUtil.GetCountryCodeForRegion(alpha2) > 0 ? alpha2 : null;
    }
}

[Obsolete("Use LibPhoneNumberNormalizer instead.")]
public sealed class BasicHotelPhoneNormalizer : IHotelPhoneNormalizer
{
    readonly LibPhoneNumberNormalizer _inner = new();
    public IReadOnlyList<NormalizedPhone> Normalize(string? raw, RegionResolution region, string? countryCode)
        => _inner.Normalize(raw, region, countryCode);
}

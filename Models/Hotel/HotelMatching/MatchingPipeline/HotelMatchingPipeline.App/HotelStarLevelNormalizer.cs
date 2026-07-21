using System.Globalization;
using System.Text.RegularExpressions;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.App;

public sealed class HotelStarLevelNormalizer : IHotelStarLevelNormalizer
{
    static readonly Regex DecoratedValue = new(
        @"^(?<value>[0-7](?:[.,][0-9]+)?)\s*(?:\*+|STARS?|EST(?:RELLAS?)?|KEYS?|LLAVES?|LL|K)(?:\s*(?:SUP|SUPERIOR|\+))?(?:\s*Y\s*MEDIA)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static readonly IReadOnlyDictionary<string, decimal> ChineseStars = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["一星"] = 1, ["二星"] = 2, ["三星"] = 3, ["四星"] = 4,
        ["五星"] = 5, ["六星"] = 6, ["七星"] = 7,
        ["一星半"] = 1.5m, ["二星半"] = 2.5m, ["三星半"] = 3.5m,
        ["四星半"] = 4.5m, ["五星半"] = 5.5m, ["六星半"] = 6.5m
    };

    static readonly IReadOnlyDictionary<string, decimal> EnglishStars = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7
    };

    readonly HotelStarLevelOptions _options;
    readonly IReadOnlyList<IHotelStarLevelConverter> _converters;

    public HotelStarLevelNormalizer(
        IOptions<HotelStarLevelOptions> options,
        IEnumerable<IHotelStarLevelConverter> converters)
    {
        _options = options.Value;
        _converters = converters.ToArray();
        if (_options.Minimum > _options.Maximum)
        {
            throw new InvalidOperationException("HotelStarLevel:Minimum must not exceed Maximum.");
        }
    }

    public HotelStarLevelNormalization Normalize(int? supplierId, string? rawValue)
    {
        var valueText = rawValue?.Trim();
        if (string.IsNullOrWhiteSpace(valueText))
        {
            return HotelStarLevelNormalization.Invalid(rawValue is null ? "missing" : "empty");
        }

        foreach (var converter in _converters)
        {
            if (converter.TryConvert(supplierId, valueText, out var mapped))
            {
                return Validate(mapped);
            }
        }

        if (ChineseStars.TryGetValue(valueText, out var chineseValue))
        {
            return Validate(chineseValue);
        }

        var englishValue = NormalizeEnglishStarText(valueText);
        if (englishValue.HasValue)
        {
            return Validate(englishValue.Value);
        }

        if (valueText.All(static ch => ch == '*'))
        {
            return Validate(valueText.Length);
        }

        if (decimal.TryParse(valueText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var numericValue))
        {
            return Validate(numericValue);
        }

        var match = DecoratedValue.Match(valueText);
        if (match.Success && decimal.TryParse(match.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var decoratedValue))
        {
            if (valueText.Contains("Y MEDIA", StringComparison.OrdinalIgnoreCase))
            {
                decoratedValue += 0.5m;
            }

            return Validate(decoratedValue);
        }

        return HotelStarLevelNormalization.Invalid("unrecognized");
    }

    static decimal? NormalizeEnglishStarText(string valueText)
    {
        var normalized = valueText
            .Replace('-', ' ')
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length < 2 || !EnglishStars.TryGetValue(words[0], out var value))
        {
            return null;
        }

        var starWordIndex = Array.FindIndex(words, static word =>
            string.Equals(word, "star", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "stars", StringComparison.OrdinalIgnoreCase));
        if (starWordIndex < 1)
        {
            return null;
        }

        var hasHalf = words.Any(static word =>
            string.Equals(word, "half", StringComparison.OrdinalIgnoreCase))
            || (words.Length >= 4
                && string.Equals(words[1], "and", StringComparison.OrdinalIgnoreCase)
                && string.Equals(words[2], "a", StringComparison.OrdinalIgnoreCase)
                && string.Equals(words[3], "half", StringComparison.OrdinalIgnoreCase));
        return hasHalf ? value + 0.5m : value;
    }

    HotelStarLevelNormalization Validate(decimal? value)
    {
        if (!value.HasValue)
        {
            return HotelStarLevelNormalization.Valid(null);
        }

        var normalized = decimal.Round(value.Value, 1, MidpointRounding.AwayFromZero);
        return normalized < _options.Minimum || normalized > _options.Maximum
            ? HotelStarLevelNormalization.Invalid($"out_of_range:{normalized.ToString(CultureInfo.InvariantCulture)}")
            : HotelStarLevelNormalization.Valid(normalized);
    }
}

public sealed class ConfiguredSupplierStarLevelConverter : IHotelStarLevelConverter
{
    readonly HotelStarLevelOptions _options;

    public ConfiguredSupplierStarLevelConverter(IOptions<HotelStarLevelOptions> options)
    {
        _options = options.Value;
    }

    public bool TryConvert(int? supplierId, string rawValue, out decimal? value)
    {
        value = null;
        if (!supplierId.HasValue || !_options.SupplierMappings.TryGetValue(supplierId.Value, out var mappings))
        {
            return false;
        }

        foreach (var mapping in mappings)
        {
            if (string.Equals(mapping.Key.Trim(), rawValue, StringComparison.OrdinalIgnoreCase))
            {
                value = mapping.Value;
                return true;
            }
        }

        return false;
    }
}

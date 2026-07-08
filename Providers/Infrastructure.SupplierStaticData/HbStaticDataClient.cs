using Infrastructure.SupplierStaticData.Configuration;
using Microsoft.Extensions.Options;
using SupplierStaticDataApp;
using System.Net;
using System.Text.Json;

namespace Infrastructure.SupplierStaticData;

public sealed class HbStaticDataClient
{
    readonly HttpClient _httpClient;
    readonly IOptionsMonitor<SupHotelStaticDataOptions> _options;

    public HbStaticDataClient(
        HttpClient httpClient,
        IOptionsMonitor<SupHotelStaticDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<HbStaticDataFetchResult> GetHotelAndRoomPayloadsAsync(
        IReadOnlyCollection<string> supplierHotelCodes,
        string? language,
        bool includeRawInfo,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = supplierHotelCodes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new HbStaticDataFetchResult();
        }

        var baseUrl = _options.CurrentValue.Url.TrimEnd('/');
        var codes = string.Join(",", normalizedCodes.Select(Uri.EscapeDataString));
        var normalizedLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        var requestUrl = $"{baseUrl}/api/hb/room/list?codes={codes}&language={Uri.EscapeDataString(normalizedLanguage)}";
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        var responseBody = await SupplierStaticHttpContentReader.ReadAsStringAsync(response, cancellationToken);
        var rawInfo = includeRawInfo
            ? new SupplierStaticDataRawInfo
            {
                Source = "direct",
                RequestUrl = requestUrl,
                ResponseStatusCode = (int)response.StatusCode,
                ResponseBody = responseBody
            }
            : null;

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return BuildFetchResult(normalizedCodes, rawInfo);
        }

        return BuildFetchResult(normalizedCodes, rawInfo, SplitPayloads(normalizedCodes, responseBody));
    }

    static IReadOnlyDictionary<string, string> SplitPayloads(IReadOnlyCollection<string> requestedCodes, string responseBody)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (TryGetProperty(document.RootElement, "data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Object)
                {
                    foreach (var code in requestedCodes)
                    {
                        if (data.TryGetProperty(code, out var item))
                        {
                            result[code] = item.GetRawText();
                        }
                    }
                }

                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var code = GetString(item, "code", "hotelCode", "hotel_code", "hotelId", "hotel_id", "sHotelCode");
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            result[code] = item.GetRawText();
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            return result;
        }

        if (result.Count == 0 && requestedCodes.Count == 1)
        {
            result[requestedCodes.First()] = responseBody;
        }

        return result;
    }

    static HbStaticDataFetchResult BuildFetchResult(
        IReadOnlyCollection<string> requestedCodes,
        SupplierStaticDataRawInfo? rawInfo,
        IReadOnlyDictionary<string, string>? payloads = null)
    {
        var rawInfos = rawInfo is null
            ? new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase)
            : requestedCodes.ToDictionary(
                code => code,
                _ => rawInfo,
                StringComparer.OrdinalIgnoreCase);

        return new HbStaticDataFetchResult
        {
            Payloads = payloads ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RawInfos = rawInfos
        };
    }

    static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        foreach (var item in element.EnumerateObject())
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    static string? GetString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var property))
            {
                return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
            }
        }

        return null;
    }
}

public sealed class HbStaticDataFetchResult
{
    public IReadOnlyDictionary<string, string> Payloads { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SupplierStaticDataRawInfo> RawInfos { get; init; }
        = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
}

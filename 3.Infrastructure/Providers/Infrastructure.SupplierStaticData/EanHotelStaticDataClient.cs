using Infrastructure.SupplierStaticData.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupplierStaticDataApp;
using System.Text.Json;

namespace Infrastructure.SupplierStaticData;

public sealed class EanHotelStaticDataClient
{
    readonly HttpClient _httpClient;
    readonly IOptionsMonitor<SupHotelStaticDataOptions> _options;
    readonly ILogger<EanHotelStaticDataClient> _logger;

    public EanHotelStaticDataClient(
        HttpClient httpClient,
        IOptionsMonitor<SupHotelStaticDataOptions> options,
        ILogger<EanHotelStaticDataClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<EanHotelStaticDataFetchResult> GetHotelPayloadsAsync(
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
            return new EanHotelStaticDataFetchResult();
        }

        var baseUrl = _options.CurrentValue.Url.TrimEnd('/');
        var normalizedLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        var requestUrl = $"{baseUrl}/api/ean/hotel/list?language={Uri.EscapeDataString(normalizedLanguage)}&codes={string.Join(",", normalizedCodes.Select(Uri.EscapeDataString))}";
        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
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

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to fetch EAN static room data. Codes={SupplierHotelCodes}, StatusCode={StatusCode}",
                string.Join(",", normalizedCodes),
                response.StatusCode);
            return BuildFetchResult(normalizedCodes, rawInfo);
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return BuildFetchResult(normalizedCodes, rawInfo);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var payloadRoot = document.RootElement;
            if (payloadRoot.ValueKind == JsonValueKind.Object
                && payloadRoot.TryGetProperty("data", out var data)
                && data.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                payloadRoot = data;
            }

            if (payloadRoot.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in payloadRoot.EnumerateObject())
                {
                    result[property.Name] = property.Value.GetRawText();
                }
            }
            else if (payloadRoot.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in payloadRoot.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var code = GetString(item, "property_id", "hotelCode", "hotel_code", "code", "id");
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        result[code] = item.GetRawText();
                    }
                }
            }

            return BuildFetchResult(normalizedCodes, rawInfo, result);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse EAN static room response. Body={Body}", responseBody);
            return BuildFetchResult(normalizedCodes, rawInfo);
        }
    }

    static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
        }

        return null;
    }

    static EanHotelStaticDataFetchResult BuildFetchResult(
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

        return new EanHotelStaticDataFetchResult
        {
            Payloads = payloads ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RawInfos = rawInfos
        };
    }
}

public sealed class EanHotelStaticDataFetchResult
{
    public IReadOnlyDictionary<string, string> Payloads { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SupplierStaticDataRawInfo> RawInfos { get; init; }
        = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
}

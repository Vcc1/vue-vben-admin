using Infrastructure.SupplierStaticData.Configuration;
using Microsoft.Extensions.Options;
using SupplierStaticDataApp;
using System.Net;

namespace Infrastructure.SupplierStaticData;

public sealed class AgodaHotelStaticDataClient
{
    readonly HttpClient _httpClient;
    readonly IOptionsMonitor<SupHotelStaticDataOptions> _options;

    public AgodaHotelStaticDataClient(
        HttpClient httpClient,
        IOptionsMonitor<SupHotelStaticDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<AgodaHotelStaticDataFetchResult> GetHotelPayloadsAsync(
        IReadOnlyCollection<string> supplierHotelCodes,
        string? language,
        bool includeRawInfo,
        CancellationToken cancellationToken)
    {
        var payloads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawInfos = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var hotelCode in supplierHotelCodes
                     .Where(static code => !string.IsNullOrWhiteSpace(code))
                     .Select(static code => code.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var baseUrl = _options.CurrentValue.Url.TrimEnd('/');
            var normalizedLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
            var requestUrl = $"{baseUrl}/api/agoda/hotel/detail/?hotelid={Uri.EscapeDataString(hotelCode)}&language={Uri.EscapeDataString(normalizedLanguage)}";
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            var responseBody = await SupplierStaticHttpContentReader.ReadAsStringAsync(response, cancellationToken);

            if (includeRawInfo)
            {
                rawInfos[hotelCode] = new SupplierStaticDataRawInfo
                {
                    Source = "direct",
                    RequestUrl = requestUrl,
                    ResponseStatusCode = (int)response.StatusCode,
                    ResponseBody = responseBody
                };
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                continue;
            }

            payloads[hotelCode] = responseBody;
        }

        return new AgodaHotelStaticDataFetchResult
        {
            Payloads = payloads,
            RawInfos = rawInfos
        };
    }
}

public sealed class AgodaHotelStaticDataFetchResult
{
    public IReadOnlyDictionary<string, string> Payloads { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SupplierStaticDataRawInfo> RawInfos { get; init; }
        = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
}

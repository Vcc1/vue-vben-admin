using Microsoft.Extensions.Options;
using Infrastructure.SupplierStaticData.Configuration;
using SupplierStaticDataApp;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.SupplierStaticData;

public sealed class AgodaRoomStaticDataClient
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    readonly HttpClient _httpClient;
    readonly IOptionsMonitor<SupHotelStaticDataOptions> _options;

    public AgodaRoomStaticDataClient(
        HttpClient httpClient,
        IOptionsMonitor<SupHotelStaticDataOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<AgodaRoomStaticDataFetchResult> GetRoomPayloadsAsync(
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
            var requestUrl = $"{baseUrl}/api/agoda/room/list/?hotelid={Uri.EscapeDataString(hotelCode)}&language={Uri.EscapeDataString(normalizedLanguage)}";
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

        return new AgodaRoomStaticDataFetchResult
        {
            Payloads = payloads,
            RawInfos = rawInfos
        };
    }

    public static AgodaRoomListResponse? Deserialize(string payload)
    {
        return JsonSerializer.Deserialize<AgodaRoomListResponse>(payload, JsonOptions);
    }
}

public sealed class AgodaRoomStaticDataFetchResult
{
    public IReadOnlyDictionary<string, string> Payloads { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SupplierStaticDataRawInfo> RawInfos { get; init; }
        = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
}

public sealed class AgodaRoomListResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<AgodaRoom> Data { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public sealed class AgodaRoom
{
    [JsonPropertyName("hotelId")]
    public long HotelId { get; set; }

    [JsonPropertyName("hotelRoomtypeId")]
    public long HotelRoomTypeId { get; set; }

    [JsonPropertyName("standardCaption")]
    public string StandardCaption { get; set; } = string.Empty;

    [JsonPropertyName("standardCaptionTranslated")]
    public string StandardCaptionTranslated { get; set; } = string.Empty;

    [JsonPropertyName("maxOccupancyPerRoom")]
    public int MaxOccupancyPerRoom { get; set; }

    [JsonPropertyName("noOfRoom")]
    public int NumberOfRoom { get; set; }

    [JsonPropertyName("sizeOfRoom")]
    public decimal SizeOfRoom { get; set; }

    [JsonPropertyName("roomSizeInclTerrace")]
    public bool RoomSizeInclTerrace { get; set; }

    [JsonPropertyName("views")]
    public string Views { get; set; } = string.Empty;

    [JsonPropertyName("maxExtrabeds")]
    public int MaxExtraBeds { get; set; }

    [JsonPropertyName("maxInfantInRoom")]
    public int MaxInfantInRoom { get; set; }

    [JsonPropertyName("hotelRoomtypePicture")]
    public string HotelRoomTypePicture { get; set; } = string.Empty;

    [JsonPropertyName("hotelRoomtypePictures")]
    public List<string> HotelRoomTypePictures { get; set; } = [];

    [JsonPropertyName("bedType")]
    public string BedType { get; set; } = string.Empty;

    [JsonPropertyName("hotelMasterRoomtypeId")]
    public long HotelMasterRoomTypeId { get; set; }

    [JsonPropertyName("hotelRoomtypeAlternateName")]
    public string HotelRoomTypeAlternateName { get; set; } = string.Empty;

    [JsonPropertyName("sharedBathroom")]
    public bool SharedBathroom { get; set; }

    [JsonPropertyName("smokingNonSmokingRoom")]
    public string SmokingNonSmoking { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

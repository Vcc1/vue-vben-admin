using Microsoft.Extensions.Logging;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Infrastructure.SupplierStaticData;

public sealed class AgodaSupplierStaticDataProvider : ISupplierStaticDataProvider
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly AgodaHotelStaticDataClient _hotelClient;
    readonly AgodaRoomStaticDataClient _roomClient;
    readonly ILogger<AgodaSupplierStaticDataProvider> _logger;

    public AgodaSupplierStaticDataProvider(
        AgodaHotelStaticDataClient hotelClient,
        AgodaRoomStaticDataClient roomClient,
        ILogger<AgodaSupplierStaticDataProvider> logger)
    {
        _hotelClient = hotelClient;
        _roomClient = roomClient;
        _logger = logger;
    }

    public int SupId => 32;

    public bool Supports(SupplierStaticDataKind dataKind) =>
        dataKind is SupplierStaticDataKind.Hotel or SupplierStaticDataKind.Room or SupplierStaticDataKind.HotelAndRoom;

    public async Task<SupplierStaticDataFetchResult> FetchHotelAndRoomsEnvelopesAsync(
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var hotelFetchResult = await _hotelClient.GetHotelPayloadsAsync(
            supplierHotelCodes,
            options.GetLanguageOrDefault(),
            options.IncludeRawInfo,
            cancellationToken);

        var roomFetchResult = await _roomClient.GetRoomPayloadsAsync(
            supplierHotelCodes,
            options.GetLanguageOrDefault(),
            options.IncludeRawInfo,
            cancellationToken);

        var envelopes = new Dictionary<string, IReadOnlyList<SupplierStaticDataEnvelope>>(StringComparer.OrdinalIgnoreCase);
        foreach (var hotelCode in supplierHotelCodes
                     .Where(static code => !string.IsNullOrWhiteSpace(code))
                     .Select(static code => code.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            hotelFetchResult.Payloads.TryGetValue(hotelCode, out var hotelPayload);
            roomFetchResult.Payloads.TryGetValue(hotelCode, out var roomPayload);
            var codeEnvelopes = new List<SupplierStaticDataEnvelope>();
            if (!string.IsNullOrWhiteSpace(hotelPayload))
            {
                codeEnvelopes.Add(new SupplierStaticDataEnvelope
                {
                    SupId = SupId,
                    DataKind = SupplierStaticDataKind.Hotel,
                    HotelCode = hotelCode,
                    FetchedAt = DateTimeOffset.Now,
                    PayloadFormat = "json",
                    Payload = hotelPayload
                });
            }

            if (!string.IsNullOrWhiteSpace(roomPayload))
            {
                codeEnvelopes.Add(new SupplierStaticDataEnvelope
                {
                    SupId = SupId,
                    DataKind = SupplierStaticDataKind.Room,
                    HotelCode = hotelCode,
                    FetchedAt = DateTimeOffset.Now,
                    PayloadFormat = "json",
                    Payload = roomPayload
                });
            }

            if (codeEnvelopes.Count == 0)
            {
                continue;
            }

            envelopes[hotelCode] = codeEnvelopes;
        }

        return new SupplierStaticDataFetchResult
        {
            Envelopes = envelopes,
            RawInfos = MergeRawInfos(hotelFetchResult.RawInfos, roomFetchResult.RawInfos)
        };
    }

    public SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto> ParseHotelAndRooms(
        string supplierHotelCode,
        SupplierStaticDataEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Payload))
        {
            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                supplierHotelCode,
                "payload_empty",
                "Supplier static data payload is empty.",
                supplierHotelCode);
        }

        try
        {
            var (hotelResponse, roomResponse) = DeserializePayload(envelope.Payload);
            if ((hotelResponse is null || !hotelResponse.Success)
                && (roomResponse is null || !roomResponse.Success))
            {
                return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                    supplierHotelCode,
                    "payload_invalid",
                    roomResponse?.Message ?? hotelResponse?.Message ?? "Supplier static data payload could not be deserialized.",
                    supplierHotelCode);
            }

            var rooms = (roomResponse?.Data ?? [])
                .Select(static room => new SupplierStaticRoomDto
                {
                    RoomCode = room.HotelRoomTypeId.ToString(),
                    Name = string.IsNullOrWhiteSpace(room.StandardCaptionTranslated)
                        ? room.StandardCaption
                        : room.StandardCaptionTranslated,
                    NameEn = room.StandardCaption,
                    MaxOccupancy = room.MaxOccupancyPerRoom == 0 ? null : room.MaxOccupancyPerRoom,
                    Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["bedType"] = room.BedType,
                        ["roomSize"] = room.SizeOfRoom == 0 ? null : room.SizeOfRoom.ToString(),
                        ["alternateName"] = room.HotelRoomTypeAlternateName,
                        ["picture"] = room.HotelRoomTypePicture,
                        ["smoking"] = room.SmokingNonSmoking,
                        ["numberOfRoom"] = room.NumberOfRoom == 0 ? null : room.NumberOfRoom.ToString()
                    }
                })
                .ToList();

            var result = new SupplierStaticHotelWithRoomsDto
            {
                Hotel = new SupplierStaticHotelDto
                {
                    SupId = SupId,
                    HotelCode = supplierHotelCode,
                    Name = hotelResponse?.Data?.HotelName,
                    RawAdministrativeArea = CreateAdministrativeArea(hotelResponse?.Data),
                    Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["address"] = hotelResponse?.Data?.Address,
                        ["longitude"] = hotelResponse?.Data?.Longitude?.ToString(),
                        ["latitude"] = hotelResponse?.Data?.Latitude?.ToString(),
                        ["phone"] = hotelResponse?.Data?.Phone,
                        ["translatedName"] = hotelResponse?.Data?.TranslatedName,
                        ["starRating"] = hotelResponse?.Data?.StarRating?.ToString(),
                        ["roomCount"] = rooms.Count.ToString()
                    }
                },
                Rooms = rooms
            };

            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.SuccessResult(
                supplierHotelCode,
                result,
                supplierHotelCode,
                fromCache: false,
                refreshed: false);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            _logger.LogWarning(ex, "Failed to parse Agoda static payload. SHotelCode={SHotelCode}", supplierHotelCode);
            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                supplierHotelCode,
                "payload_invalid",
                "Supplier static data payload could not be parsed.",
                supplierHotelCode);
        }
    }

    static string BuildCombinedPayload(string? hotelPayload, string? roomPayload)
    {
        var node = new JsonObject
        {
            ["hotel"] = ParseNodeOrNull(hotelPayload),
            ["rooms"] = ParseNodeOrNull(roomPayload)
        };

        return node.ToJsonString(JsonOptions);
    }

    static JsonNode? ParseNodeOrNull(string? payload)
    {
        return string.IsNullOrWhiteSpace(payload) ? null : JsonNode.Parse(payload);
    }

    static (AgodaHotelDetailResponse? Hotel, AgodaRoomListResponse? Rooms) DeserializePayload(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("hotel", out var hotelElement)
            && document.RootElement.TryGetProperty("rooms", out var roomsElement))
        {
            return (
                hotelElement.ValueKind == JsonValueKind.Null
                    ? null
                    : JsonSerializer.Deserialize<AgodaHotelDetailResponse>(hotelElement.GetRawText(), JsonOptions),
                roomsElement.ValueKind == JsonValueKind.Null
                    ? null
                    : JsonSerializer.Deserialize<AgodaRoomListResponse>(roomsElement.GetRawText(), JsonOptions));
        }

        return (null, AgodaRoomStaticDataClient.Deserialize(payload));
    }

    static IReadOnlyDictionary<string, SupplierStaticDataRawInfo> MergeRawInfos(
        IReadOnlyDictionary<string, SupplierStaticDataRawInfo> hotelRawInfos,
        IReadOnlyDictionary<string, SupplierStaticDataRawInfo> roomRawInfos)
    {
        var result = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in hotelRawInfos)
        {
            result[pair.Key] = new SupplierStaticDataRawInfo
            {
                Source = "direct",
                RequestUrl = pair.Value.RequestUrl,
                ResponseStatusCode = pair.Value.ResponseStatusCode,
                ResponseBody = pair.Value.ResponseBody,
                Items = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hotel"] = pair.Value
                }
            };
        }

        foreach (var pair in roomRawInfos)
        {
            if (!result.TryGetValue(pair.Key, out var existing))
            {
                result[pair.Key] = new SupplierStaticDataRawInfo
                {
                    Source = "direct",
                    RequestUrl = pair.Value.RequestUrl,
                    ResponseStatusCode = pair.Value.ResponseStatusCode,
                    ResponseBody = pair.Value.ResponseBody,
                    Items = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["rooms"] = pair.Value
                    }
                };
                continue;
            }

            var items = existing.Items is null
                ? new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, SupplierStaticDataRawInfo>(existing.Items, StringComparer.OrdinalIgnoreCase);
            items["rooms"] = pair.Value;

            result[pair.Key] = new SupplierStaticDataRawInfo
            {
                Source = "direct",
                RequestUrl = $"{existing.RequestUrl}\n{pair.Value.RequestUrl}",
                ResponseStatusCode = pair.Value.ResponseStatusCode ?? existing.ResponseStatusCode,
                ResponseBody = new JsonObject
                {
                    ["hotel"] = ParseNodeOrString(existing.ResponseBody),
                    ["rooms"] = ParseNodeOrString(pair.Value.ResponseBody)
                }.ToJsonString(JsonOptions),
                Items = items
            };
        }

        return result;
    }

    static JsonNode? ParseNodeOrString(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return JsonValue.Create(payload);
        }
    }

    static SupplierStaticGeoAdministrativeAreaDto? CreateAdministrativeArea(AgodaHotelDetail? hotel)
    {
        if (hotel is null)
        {
            return null;
        }

        var area = new SupplierStaticGeoAdministrativeAreaDto
        {
            CountryCode = hotel.CountryId?.ToString(CultureInfo.InvariantCulture),
            CityCode = hotel.CityId?.ToString(CultureInfo.InvariantCulture),
            DistrictCode = hotel.AreaId?.ToString(CultureInfo.InvariantCulture)
        };

        return new[] { area.CountryCode, area.CityCode, area.DistrictCode }
            .Any(static value => !string.IsNullOrWhiteSpace(value))
            ? area
            : null;
    }
}

public sealed class AgodaHotelDetailResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public AgodaHotelDetail? Data { get; set; }

    public int Total { get; set; }
}

public sealed class AgodaHotelDetail
{
    [JsonPropertyName("hotelId")]
    public long HotelId { get; set; }

    [JsonPropertyName("hotelName")]
    public string HotelName { get; set; } = string.Empty;

    [JsonPropertyName("translatedName")]
    public string TranslatedName { get; set; } = string.Empty;

    [JsonPropertyName("starRating")]
    public decimal? StarRating { get; set; }

    [JsonPropertyName("longitude")]
    public decimal? Longitude { get; set; }

    [JsonPropertyName("latitude")]
    public decimal? Latitude { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("countryId")]
    public long? CountryId { get; set; }

    [JsonPropertyName("cityId")]
    public long? CityId { get; set; }

    [JsonPropertyName("areaId")]
    public long? AreaId { get; set; }
}

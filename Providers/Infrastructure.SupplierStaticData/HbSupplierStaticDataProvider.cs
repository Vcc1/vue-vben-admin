using Microsoft.Extensions.Logging;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;
using System.Text.Json;

namespace Infrastructure.SupplierStaticData;

public sealed class HbSupplierStaticDataProvider : ISupplierStaticDataProvider
{
    readonly HbStaticDataClient _client;
    readonly ILogger<HbSupplierStaticDataProvider> _logger;

    public HbSupplierStaticDataProvider(
        HbStaticDataClient client,
        ILogger<HbSupplierStaticDataProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public int SupId => 1;

    public bool Supports(SupplierStaticDataKind dataKind) =>
        dataKind is SupplierStaticDataKind.Hotel or SupplierStaticDataKind.Room or SupplierStaticDataKind.HotelAndRoom;

    public async Task<SupplierStaticDataFetchResult> FetchHotelAndRoomsEnvelopesAsync(
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var fetchResult = await _client.GetHotelAndRoomPayloadsAsync(
            supplierHotelCodes,
            options.GetLanguageOrDefault(),
            options.IncludeRawInfo,
            cancellationToken);

        var envelopes = fetchResult.Payloads.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<SupplierStaticDataEnvelope>)[new SupplierStaticDataEnvelope
            {
                SupId = SupId,
                DataKind = SupplierStaticDataKind.HotelAndRoom,
                HotelCode = pair.Key,
                FetchedAt = DateTimeOffset.Now,
                PayloadFormat = "json",
                Payload = pair.Value
            }],
            StringComparer.OrdinalIgnoreCase);

        return new SupplierStaticDataFetchResult
        {
            Envelopes = envelopes,
            RawInfos = fetchResult.RawInfos
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
            using var document = JsonDocument.Parse(envelope.Payload);
            var root = UnwrapData(document.RootElement);
            var roomsElement = FindRoomsElement(root);
            var rooms = ReadRooms(roomsElement).ToList();
            var hotel = new SupplierStaticHotelDto
            {
                SupId = SupId,
                HotelCode = GetString(root, "code", "hotelCode", "hotel_code", "hotelId", "hotel_id", "sHotelCode") ?? supplierHotelCode,
                Name = GetString(root, "name", "hotelName", "hotel_name", "nameEn", "name_en"),
                RawAdministrativeArea = CreateAdministrativeArea(root),
                Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["address"] = GetString(root, "address", "hotelAddress", "hotel_address"),
                    ["longitude"] = GetDecimal(root, "coordinates.longitude", "longitude")?.ToString(),
                    ["latitude"] = GetDecimal(root, "coordinates.latitude", "latitude")?.ToString(),
                    ["starRating"] = GetString(root, "S2C", "starRating", "star_rating", "categoryCode", "category_code"),
                    ["phone"] = GetPhone(root),
                    ["email"] = GetString(root, "email"),
                    ["website"] = GetString(root, "web", "website"),
                    ["roomCount"] = rooms.Count.ToString()
                }
            };

            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.SuccessResult(
                supplierHotelCode,
                new SupplierStaticHotelWithRoomsDto
                {
                    Hotel = hotel,
                    Rooms = rooms
                },
                supplierHotelCode,
                fromCache: false,
                refreshed: false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse HB static payload. SHotelCode={SHotelCode}", supplierHotelCode);
            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                supplierHotelCode,
                "payload_invalid",
                "Supplier static data payload could not be parsed.",
                supplierHotelCode);
        }
    }

    static JsonElement UnwrapData(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && TryGetProperty(root, "data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                return data.EnumerateArray().FirstOrDefault();
            }

            return data;
        }

        return root;
    }

    static JsonElement FindRoomsElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        foreach (var name in new[] { "rooms", "Rooms", "roomList", "room_list", "hotelRooms", "hotel_rooms" })
        {
            if (TryGetProperty(root, name, out var rooms))
            {
                return rooms;
            }
        }

        return default;
    }

    static IEnumerable<SupplierStaticRoomDto> ReadRooms(JsonElement roomsElement)
    {
        if (roomsElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var room in roomsElement.EnumerateArray())
        {
            yield return new SupplierStaticRoomDto
            {
                RoomCode = GetString(room, "code", "roomCode", "room_code", "id", "roomId", "room_id") ?? string.Empty,
                Name = GetString(room, "name", "roomName", "room_name", "nameCn", "name_cn"),
                NameEn = GetString(room, "nameEn", "name_en", "roomNameEn", "room_name_en", "name", "roomName", "room_name"),
                MaxOccupancy = GetInt(room, "maxOccupancy", "max_occupancy", "maxAdult", "max_adult", "maxAdults", "max_adults", "maxPax", "max_pax")
                    ?? InferMaxOccupancyFromHbRoomCode(GetString(room, "code", "roomCode", "room_code", "id", "roomId", "room_id")),
                Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bedType"] = GetString(room, "bedType", "bed_type", "bed"),
                    ["roomSize"] = GetString(room, "roomSize", "room_size", "size"),
                    ["rawCode"] = GetString(room, "code", "roomCode", "room_code", "id", "roomId", "room_id")
                }
            };
        }
    }

    static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

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

    static SupplierStaticGeoAdministrativeAreaDto? CreateAdministrativeArea(JsonElement root)
    {
        var area = new SupplierStaticGeoAdministrativeAreaDto
        {
            CountryCode = GetString(root, "countryCode", "country_code", "country.code", "country.isoCode"),
            CountryName = GetString(root, "countryName", "country_name", "country.name", "country.content"),
            ProvinceCode = GetString(root, "stateCode", "state_code", "state.code", "provinceCode", "province_code"),
            ProvinceName = GetString(root, "stateName", "state_name", "state.name", "provinceName", "province_name", "province.content"),
            CityCode = GetString(root, "cityCode", "city_code", "city.code", "destinationCode", "destination_code", "destination.code"),
            CityName = GetString(root, "city", "cityName", "city_name", "city.content", "destinationName", "destination_name", "destination.name"),
            DistrictCode = GetString(root, "zoneCode", "zone_code", "zone.code", "districtCode", "district_code"),
            DistrictName = GetString(root, "zoneName", "zone_name", "zone.name", "districtName", "district_name", "district.content")
        };

        return HasAnyAdministrativeAreaValue(area) ? area : null;
    }

    static bool HasAnyAdministrativeAreaValue(SupplierStaticGeoAdministrativeAreaDto area)
    {
        return new[]
        {
            area.CountryCode,
            area.CountryName,
            area.ProvinceCode,
            area.ProvinceName,
            area.CityCode,
            area.CityName,
            area.DistrictCode,
            area.DistrictName
        }.Any(static value => !string.IsNullOrWhiteSpace(value));
    }

    static bool TryGetPath(JsonElement element, string path, out JsonElement property)
    {
        property = element;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryGetProperty(property, segment, out property))
            {
                return false;
            }
        }

        return true;
    }

    static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetPath(element, name, out var property))
            {
                return property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString(),
                    JsonValueKind.Number => property.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Object => GetString(property, "content", "name", "value"),
                    _ => null
                };
            }
        }

        return null;
    }

    static decimal? GetDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetPath(element, name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String
                && decimal.TryParse(property.GetString(), out var stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    static string? GetPhone(JsonElement element)
    {
        if (!TryGetProperty(element, "phones", out var phones) || phones.ValueKind != JsonValueKind.Array)
        {
            return GetString(element, "phone", "phoneNumber", "telephone");
        }

        var phoneNumbers = phones
            .EnumerateArray()
            .Select(static phone => GetString(phone, "phoneNumber", "number", "phone"))
            .Where(static phone => !string.IsNullOrWhiteSpace(phone))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return phoneNumbers.Length == 0 ? null : string.Join(",", phoneNumbers);
    }

    static int? GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String
                && int.TryParse(property.GetString(), out var stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    static int? InferMaxOccupancyFromHbRoomCode(string? roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return null;
        }

        var roomType = roomCode.Split('.', '-', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant();
        return roomType switch
        {
            "SGL" => 1,
            "DBL" => 2,
            "DBT" => 2,
            "TWN" => 2,
            "TPL" => 3,
            "TRP" => 3,
            "QUA" => 4,
            "FAM" => 4,
            _ => null
        };
    }
}

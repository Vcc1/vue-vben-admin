using Microsoft.Extensions.Logging;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Infrastructure.SupplierStaticData;

public sealed class EanSupplierStaticDataProvider : ISupplierStaticDataProvider
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly EanHotelStaticDataClient _client;
    readonly ILogger<EanSupplierStaticDataProvider> _logger;

    public EanSupplierStaticDataProvider(
        EanHotelStaticDataClient client,
        ILogger<EanSupplierStaticDataProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public int SupId => 51;

    public bool Supports(SupplierStaticDataKind dataKind) => dataKind is SupplierStaticDataKind.Hotel or SupplierStaticDataKind.Room or SupplierStaticDataKind.HotelAndRoom;

    public async Task<SupplierStaticDataFetchResult> FetchHotelAndRoomsEnvelopesAsync(
        IReadOnlyCollection<string> supplierHotelCodes,
        SupplierStaticDataRequestOptions options,
        CancellationToken cancellationToken)
    {
        var envelopes = new Dictionary<string, IReadOnlyList<SupplierStaticDataEnvelope>>(StringComparer.OrdinalIgnoreCase);
        var rawInfos = new Dictionary<string, SupplierStaticDataRawInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in supplierHotelCodes
                     .Where(static code => !string.IsNullOrWhiteSpace(code))
                     .Select(static code => code.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Chunk(100))
        {
            var fetchResult = await _client.GetHotelPayloadsAsync(batch, options.GetLanguageOrDefault(), options.IncludeRawInfo, cancellationToken);
            foreach (var pair in fetchResult.Payloads)
            {
                envelopes[pair.Key] = [new SupplierStaticDataEnvelope
                {
                    SupId = SupId,
                    DataKind = SupplierStaticDataKind.HotelAndRoom,
                    HotelCode = pair.Key,
                    FetchedAt = DateTimeOffset.Now,
                    PayloadFormat = "json",
                    Payload = pair.Value
                }];
            }

            foreach (var pair in fetchResult.RawInfos)
            {
                rawInfos[pair.Key] = pair.Value;
            }
        }

        return new SupplierStaticDataFetchResult
        {
            Envelopes = envelopes,
            RawInfos = rawInfos
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
            var hotel = JsonSerializer.Deserialize<EanHotelDto>(envelope.Payload, JsonOptions);
            if (hotel is null)
            {
                return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                    supplierHotelCode,
                    "payload_invalid",
                    "Supplier static data payload could not be deserialized.",
                    supplierHotelCode);
            }

            var rooms = hotel.Rooms?
                .Select(pair => new SupplierStaticRoomDto
                {
                    RoomCode = pair.Key,
                    Name = pair.Value.Name,
                    NameEn = pair.Value.Name,
                    MaxOccupancy = pair.Value.Occupancy?.MaxAllowed?.Total,
                    Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["bedInfo"] = pair.Value.BedGroups?.Values.FirstOrDefault()?.Description
                    }
                })
                .ToList() ?? [];

            var hotelDto = new SupplierStaticHotelWithRoomsDto
            {
                Hotel = new SupplierStaticHotelDto
                {
                    SupId = SupId,
                    HotelCode = supplierHotelCode,
                    Name = hotel.Name,
                    RawAdministrativeArea = CreateAdministrativeArea(hotel.Address),
                    Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["propertyId"] = hotel.PropertyId,
                        ["address"] = BuildAddress(hotel.Address),
                        ["longitude"] = hotel.Location?.Coordinates?.Longitude?.ToString(),
                        ["latitude"] = hotel.Location?.Coordinates?.Latitude?.ToString(),
                        ["starRating"] = hotel.Ratings?.Property?.Rating,
                        ["phone"] = hotel.Phone,
                        ["postalCode"] = hotel.Address?.PostalCode,
                        ["category"] = hotel.Category?.Name
                    }
                },
                Rooms = rooms
            };

            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.SuccessResult(
                supplierHotelCode,
                hotelDto,
                supplierHotelCode,
                fromCache: false,
                refreshed: false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse EAN hotel payload. SHotelCode={SHotelCode}", supplierHotelCode);
            return SupplierStaticDataResult<SupplierStaticHotelWithRoomsDto>.Failure(
                supplierHotelCode,
                "payload_invalid",
                "Supplier static data payload could not be parsed.",
                supplierHotelCode);
        }
    }

    static string? BuildAddress(EanAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        return string.Join(
            ", ",
            new[]
            {
                address.Line1,
                address.Line2,
                address.City,
                address.StateProvinceName,
                address.PostalCode,
                address.CountryCode
            }.Where(static item => !string.IsNullOrWhiteSpace(item)));
    }

    static SupplierStaticGeoAdministrativeAreaDto? CreateAdministrativeArea(EanAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        var area = new SupplierStaticGeoAdministrativeAreaDto
        {
            CountryCode = address.CountryCode,
            ProvinceCode = address.StateProvinceCode,
            ProvinceName = address.StateProvinceName,
            CityName = address.City
        };

        return new[] { area.CountryCode, area.ProvinceCode, area.ProvinceName, area.CityName }
            .Any(static value => !string.IsNullOrWhiteSpace(value))
            ? area
            : null;
    }
}

using Framework.Core.IOC;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.HTTP.SupStaticData.Agoda
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient)]
    public class AgodaStaticDataContext
    {
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        private const string InternetBaseUrl = "http://47.98.144.227:8000/api/agoda/room/list/";
        private const string LanBaseUrl = "http://10.111.1.60:8000/api/agoda/room/list/";

        public AgodaStaticDataContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string BaseUrl => _configuration.GetValue<string>("MengTuDBConfig:Sup_DbIsUseLAN") == "Y"
            ? LanBaseUrl
            : InternetBaseUrl;

        public async Task<AgodaRoomListResponse> GetRoomListAsync(string hotelId, string language = "en", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hotelId))
            {
                throw new ArgumentException("hotelId is required", nameof(hotelId));
            }

            var url = $"{BaseUrl}?hotelid={hotelId}&language={language}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<AgodaRoomListResponse>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);
            return result ?? new AgodaRoomListResponse();
        }



    }

    public class AgodaRoomListResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<AgodaRoom> Data { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class AgodaRoom
    {
        [JsonPropertyName("hotel_id")]
        public string HotelId { get; set; } = string.Empty;

        [JsonPropertyName("hotel_room_type_id")]
        public long HotelRoomTypeId { get; set; }

        [JsonPropertyName("standard_caption")]
        public string StandardCaption { get; set; } = string.Empty;

        [JsonPropertyName("standard_caption_translated")]
        public string StandardCaptionTranslated { get; set; } = string.Empty;

        [JsonPropertyName("max_occupancy_per_room")]
        public int MaxOccupancyPerRoom { get; set; }

        [JsonPropertyName("no_of_room")]
        public int NumberOfRoom { get; set; }

        [JsonPropertyName("size_of_room")]
        public decimal SizeOfRoom { get; set; }

        [JsonPropertyName("room_size_incl_terrace")]
        public string RoomSizeInclTerrace { get; set; } = string.Empty;

        [JsonPropertyName("views")]
        public string Views { get; set; } = string.Empty;

        [JsonPropertyName("max_extrabeds")]
        public int MaxExtraBeds { get; set; }

        [JsonPropertyName("max_infant_in_room")]
        public int MaxInfantInRoom { get; set; }

        [JsonPropertyName("hotel_room_type_picture")]
        public string HotelRoomTypePicture { get; set; } = string.Empty;

        [JsonPropertyName("bed_type")]
        public string BedType { get; set; } = string.Empty;

        [JsonPropertyName("hotel_master_room_type_id")]
        public long HotelMasterRoomTypeId { get; set; }

        [JsonPropertyName("hotel_room_type_alternate_name")]
        public string HotelRoomTypeAlternateName { get; set; } = string.Empty;

        [JsonPropertyName("shared_bathroom")]
        public string SharedBathroom { get; set; } = string.Empty;

        [JsonPropertyName("smoking_nonsmoking")]
        public string SmokingNonSmoking { get; set; } = string.Empty;

        [JsonPropertyName("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }


}

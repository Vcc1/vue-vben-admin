using Framework.Core.IOC;
using Microsoft.Extensions.Configuration;
using Sup.InterFace.SupBaseApi.EANEPS.Entitys.EanHotelInfo;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.HTTP.SupStaticData.EAN
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient)]
    public class EANStaticDataContext
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = CreateJsonSerializerOptions();
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        });

        private const string InternetBaseUrl = "http://47.98.144.227:8000/api/ean/hotel/list/";
        private const string LanBaseUrl = "http://10.111.1.60:8000/api/ean/hotel/list/";

        public EANStaticDataContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string BaseUrl => _configuration.GetValue<string>("MengTuDBConfig:Sup_DbIsUseLAN") == "Y"
            ? LanBaseUrl
            : InternetBaseUrl;

        public async Task<EANHotelListResponse> GetHotelListAsync(IEnumerable<string> hotelIds, string language = "en", CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(hotelIds);

            var normalizedHotelIds = new List<string>();
            foreach (var hotelId in hotelIds)
            {
                if (string.IsNullOrWhiteSpace(hotelId))
                {
                    continue;
                }

                normalizedHotelIds.Add(hotelId.Trim());
            }

            if (normalizedHotelIds.Count == 0)
            {
                throw new ArgumentException("At least one hotelId is required", nameof(hotelIds));
            }

            if (normalizedHotelIds.Count > 100)
            {
                throw new ArgumentException("A maximum of 100 hotelIds is allowed", nameof(hotelIds));
            }

            var joinedHotelIds = string.Join(",", normalizedHotelIds);
            var requestLanguage = Uri.EscapeDataString(language);
            var url = $"{BaseUrl}?codes={joinedHotelIds}&language={requestLanguage}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var str = await response.Content.ReadAsStringAsync(cancellationToken);
            //File.WriteAllText("c:/mp4/1.txt", str);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var result = await JsonSerializer.DeserializeAsync<EANHotelListResponse>(stream, JsonSerializerOptions, cancellationToken);
            return result ?? new EANHotelListResponse();
        }

        private static JsonSerializerOptions CreateJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new EANHotelInfoListConverter());
            return options;
        }


    }

    public class EANHotelListResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public Dictionary<string, EPSHotelInfoRespone> Data { get; set; } = new();

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    internal sealed class EANHotelInfoListConverter : JsonConverter<List<EPSHotelInfoRespone>>
    {
        public override List<EPSHotelInfoRespone> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var hotels = new List<EPSHotelInfoRespone>();

            if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
            {
                return hotels;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    AddHotelInfo(item, null, hotels, options);
                }

                return hotels;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (LooksLikeHotelInfo(root))
                {
                    AddHotelInfo(root, null, hotels, options);
                    return hotels;
                }

                foreach (var property in root.EnumerateObject())
                {
                    AddHotelInfo(property.Value, property.Name, hotels, options);
                }

                return hotels;
            }

            throw new JsonException($"Unsupported data JSON type: {root.ValueKind}");
        }

        public override void Write(Utf8JsonWriter writer, List<EPSHotelInfoRespone> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }

        private static void AddHotelInfo(JsonElement element, string? fallbackPropertyId, List<EPSHotelInfoRespone> hotels, JsonSerializerOptions options)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var hotel = JsonSerializer.Deserialize<EPSHotelInfoRespone>(element.GetRawText(), options);
            if (hotel == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(hotel.PropertyId) && !string.IsNullOrWhiteSpace(fallbackPropertyId))
            {
                hotel.PropertyId = fallbackPropertyId;
            }

            hotels.Add(hotel);
        }

        private static bool LooksLikeHotelInfo(JsonElement element)
        {
            return element.TryGetProperty("property_id", out _)
                || element.TryGetProperty("name", out _)
                || element.TryGetProperty("rooms", out _)
                || element.TryGetProperty("rates", out _);
        }
    }



}

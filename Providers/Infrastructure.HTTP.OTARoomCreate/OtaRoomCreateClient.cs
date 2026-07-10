using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OTARoomBuilder;
using OTARoomBuilder.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Infrastructure.HTTP.OTARoomCreate;

public sealed class OtaRoomCreateClient : IOtaRoomCreateClient
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    readonly HttpClient _httpClient;
    readonly IOptionsMonitor<RoomCreateOptions> _options;
    readonly OTARoomBuilderMetrics _metrics;
    readonly ILogger<OtaRoomCreateClient> _logger;

    public OtaRoomCreateClient(
        HttpClient httpClient,
        IOptionsMonitor<RoomCreateOptions> options,
        OTARoomBuilderMetrics metrics,
        ILogger<OtaRoomCreateClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task PostAsync(IReadOnlyList<OTARoomInfoPre> rooms, CancellationToken cancellationToken)
    {
        var roomCreateOptions = _options.CurrentValue;

        foreach (var room in rooms)
        {
            var payload = new
            {
                mthotelCode = room.MTHotelCode,
                roomNameCN = room.RoomNameCN,
                roomNameEN = string.IsNullOrWhiteSpace(room.RoomNameEN) ? room.RoomNameCN : room.RoomNameEN,
                maxPerson = room.Person,
                acreageFrom = room.AreaMin,
                acreageTo = room.AreaMax,
                smoking = room.Smoke,
                creator = "System",
                supRoomId = room.Source,
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");
            content.Headers.Add("ApiToken", roomCreateOptions.ApiToken);

            try
            {
                var response = await _httpClient.PostAsync(roomCreateOptions.Url, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _metrics.RecordError("post_room");
                    _logger.LogWarning(
                        "Failed to post OTARoomInfoPre. MTHotelCode={MTHotelCode}, RoomNameCN={RoomNameCN}, StatusCode={StatusCode}",
                        room.MTHotelCode,
                        room.RoomNameCN,
                        response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _metrics.RecordError("post_room");
                _logger.LogError(ex, "Failed to post OTARoomInfoPre. MTHotelCode={MTHotelCode}", room.MTHotelCode);
            }
        }
    }
}

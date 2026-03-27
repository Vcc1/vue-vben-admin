using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class HttpExpediaChaseRoomTaskSource : IExpediaChaseRoomTaskSource
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<ExpediaChaseRoomAppOptions> _optionsMonitor;
    private readonly ILogger<HttpExpediaChaseRoomTaskSource> _logger;

    public HttpExpediaChaseRoomTaskSource(
        HttpClient httpClient,
        IOptionsMonitor<ExpediaChaseRoomAppOptions> optionsMonitor,
        ILogger<HttpExpediaChaseRoomTaskSource> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExpediaChaseRoomTask>> FetchPendingTasksAsync(CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var requestUrl = BuildRequestUrl(options.GetExpediaChaseRoomHotelUrl, options.FetchTake);
        using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var sourceItems = await response.Content.ReadFromJsonAsync<List<AsyncChaseRoomHotelDto>>(JsonSerializerOptions, cancellationToken)
            ?? new List<AsyncChaseRoomHotelDto>();

        var today = DateTime.Today;
        var tasks = sourceItems
            .Where(item => item.CheckIn >= today && item.CheckOut > item.CheckIn)
            .Select(item => new ExpediaChaseRoomTask
            {
                PropertyId = item.SHotelCode,
                MTHotelCode = item.MTHotelCode,
                CheckIn = item.CheckIn,
                CheckOut = item.CheckOut,
                Rooms = item.RoomNum,
                Adults = item.Adult
            })
            .GroupBy(item => item.Key)
            .Select(group => group.First())
            .ToList();

        _logger.LogInformation("Expedia 追房任务拉取完成，获取 {TaskCount} 条任务。", tasks.Count);
        return tasks;
    }

    private static string BuildRequestUrl(string baseUrl, int take)
    {
        if (take <= 0)
        {
            return baseUrl;
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}take={take}";
    }
}
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelMatchingPipeline.App.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.Infra.ThirdParty;

public sealed class ThirdPartyHotelMatchOptions
{
    public const string SectionName = "ThirdPartyHotelMatch";

    public string Endpoint { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class ThirdPartyHotelMatchClient : IThirdPartyHotelMatchClient
{
    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    readonly HttpClient _httpClient;
    readonly IOptions<ThirdPartyHotelMatchOptions> _options;
    readonly ILogger<ThirdPartyHotelMatchClient> _logger;

    public ThirdPartyHotelMatchClient(
        HttpClient httpClient,
        IOptions<ThirdPartyHotelMatchOptions> options,
        ILogger<ThirdPartyHotelMatchClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds));
    }

    public async Task<HotelMatchDeliveryReceipt> DeliverAsync(
        HotelMatchDeliveryCommand command,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return new HotelMatchDeliveryReceipt(false, null, "third_party_endpoint_not_configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(command, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", command.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {options.ApiKey}");
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Third-party hotel match delivery failed. StatusCode={StatusCode}, SupId={SupId}, SHotelCode={SHotelCode}", response.StatusCode, command.SupId, command.SHotelCode);
                return new HotelMatchDeliveryReceipt(false, body, $"http_{(int)response.StatusCode}");
            }

            return new HotelMatchDeliveryReceipt(true, body, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Third-party hotel match delivery request failed. SupId={SupId}, SHotelCode={SHotelCode}", command.SupId, command.SHotelCode);
            return new HotelMatchDeliveryReceipt(false, null, ex.Message);
        }
    }

    static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

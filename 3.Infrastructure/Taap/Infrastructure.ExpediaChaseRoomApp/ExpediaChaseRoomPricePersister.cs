using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infrastructure.Redis.SupPriceCahce;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomPricePersister : IExpediaChaseRoomPricePersister
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new DateTimeJsonConverter(),
            new NullableDateTimeJsonConverter()
        }
    };

    private static readonly Lock ChargeTitleSyncRoot = new();
    private static readonly Lock UnknownPromoSyncRoot = new();
    private static HashSet<string> _chargeTitles = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _unknownPromoLabels = new(StringComparer.OrdinalIgnoreCase);

    private readonly SupPriceCahceRedis _priceCacheRedis;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IExpediaTaapPromoLabelProvider _promoLabelProvider;
    private readonly IOptionsMonitor<ExpediaChaseRoomAppOptions> _optionsMonitor;
    private readonly ILogger<ExpediaChaseRoomPricePersister> _logger;

    public ExpediaChaseRoomPricePersister(
        SupPriceCahceRedis priceCacheRedis,
        IHttpClientFactory httpClientFactory,
        IExpediaTaapPromoLabelProvider promoLabelProvider,
        IOptionsMonitor<ExpediaChaseRoomAppOptions> optionsMonitor,
        ILogger<ExpediaChaseRoomPricePersister> logger)
    {
        _priceCacheRedis = priceCacheRedis;
        _httpClientFactory = httpClientFactory;
        _promoLabelProvider = promoLabelProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<ExpediaChaseRoomPricePersistResult> PersistAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaChaseRoomTask task,
        ExpediaTaapPriceQueryResult result,
        CancellationToken cancellationToken = default)
    {
        var appOptions = _optionsMonitor.CurrentValue;
        var promoLabels = _promoLabelProvider.GetPromoLabels(account.AccountId);
        var parsedResult = ExpediaChaseRoomPriceParser.Parse(task, result.RawContent, account.SigninEmailAddress, promoLabels);

        await SavePricesAsync(task, parsedResult.Prices);
        await SaveChargeTitlesAsync(parsedResult.ChargeTitles);
        await SaveUnknownPromoLabelsAsync(parsedResult.UnknownPromoLabels);

        if (task.Priority == 9 && parsedResult.Prices.Count > 0)
        {
            await PushCompletePriceCheckAsync(task, appOptions, cancellationToken);
        }

        return new ExpediaChaseRoomPricePersistResult(parsedResult.Prices.Count > 0);
    }

    private async Task SavePricesAsync(ExpediaChaseRoomTask task, IReadOnlyList<ExpediaChaseRoomPriceRecord> prices)
    {
        var redisKey = $"{task.PropertyId}-{task.CheckIn:yyyy-MM-dd}-{task.CheckOut:yyyy-MM-dd}";
        var hashField = $"{task.Rooms}-{task.Adults}";
        var payload = JsonSerializer.Serialize(prices, SerializerOptions);

        await _priceCacheRedis.SavePricePayloadAsync(redisKey, hashField, payload, TimeSpan.FromHours(8));
    }

    private async Task SaveChargeTitlesAsync(IReadOnlySet<string> chargeTitles)
    {
        if (chargeTitles.Count == 0)
        {
            return;
        }

        await EnsureChargeTitlesInitializedAsync();

        var newTitles = chargeTitles
            .Where(title => !_chargeTitles.Contains(title))
            .ToArray();
        if (newTitles.Length == 0)
        {
            return;
        }

        await _priceCacheRedis.AddChargeTitlesAsync(newTitles);

        lock (ChargeTitleSyncRoot)
        {
            foreach (var title in newTitles)
            {
                _chargeTitles.Add(title);
            }
        }
    }

    private async Task SaveUnknownPromoLabelsAsync(IReadOnlySet<string> labels)
    {
        if (labels.Count == 0)
        {
            return;
        }

        await EnsureUnknownPromoLabelsInitializedAsync();

        var newLabels = labels
            .Where(label => !_unknownPromoLabels.Contains(label))
            .ToArray();
        if (newLabels.Length == 0)
        {
            return;
        }

        await _priceCacheRedis.AddUnknownPropertiesAsync(newLabels);

        lock (UnknownPromoSyncRoot)
        {
            foreach (var label in newLabels)
            {
                _unknownPromoLabels.Add(label);
            }
        }
    }

    private async Task EnsureChargeTitlesInitializedAsync()
    {
        if (_chargeTitles.Count > 0)
        {
            return;
        }

        var values = await _priceCacheRedis.GetChargeTitlesAsync();
        lock (ChargeTitleSyncRoot)
        {
            if (_chargeTitles.Count == 0)
            {
                _chargeTitles = values;
            }
        }
    }

    private async Task EnsureUnknownPromoLabelsInitializedAsync()
    {
        if (_unknownPromoLabels.Count > 0)
        {
            return;
        }

        var values = await _priceCacheRedis.GetUnknownPropertiesAsync();
        lock (UnknownPromoSyncRoot)
        {
            if (_unknownPromoLabels.Count == 0)
            {
                _unknownPromoLabels = values;
            }
        }
    }

    private async Task PushCompletePriceCheckAsync(
        ExpediaChaseRoomTask task,
        ExpediaChaseRoomAppOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.PushCompletePriceCheck) ||
            string.IsNullOrWhiteSpace(options.PushCompletePriceCheckAppId) ||
            string.IsNullOrWhiteSpace(options.PushCompletePriceCheckAppKey) ||
            string.IsNullOrWhiteSpace(task.MTHotelCode))
        {
            return;
        }

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var sign = CalculateMd5($"{options.PushCompletePriceCheckAppId}{options.PushCompletePriceCheckAppKey}{timestamp}");
            using var request = new HttpRequestMessage(HttpMethod.Post, options.PushCompletePriceCheck)
            {
                Content = JsonContent.Create(new
                {
                    MTHotelCode = task.MTHotelCode,
                    CheckIn = task.CheckIn.ToString("yyyy-MM-dd"),
                    CheckOut = task.CheckOut.ToString("yyyy-MM-dd"),
                    RoomNumber = task.Rooms,
                    Adults = task.Adults
                })
            };
            request.Headers.TryAddWithoutValidation("Timestamp", timestamp);
            request.Headers.TryAddWithoutValidation("Sign", sign);
            request.Headers.TryAddWithoutValidation("AppId", options.PushCompletePriceCheckAppId);

            using var client = _httpClientFactory.CreateClient("ExpediaChaseRoomPriceCallback");
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送全量验价完成回调失败，任务 {TaskId}。", task.Id);
        }
    }

    private static string CalculateMd5(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var item in bytes)
        {
            builder.Append(item.ToString("x2"));
        }

        return builder.ToString();
    }
}

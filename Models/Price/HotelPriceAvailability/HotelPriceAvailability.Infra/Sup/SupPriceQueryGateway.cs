using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelPriceAvailability.App.Contracts;
using HotelPriceAvailability.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelPriceAvailability.Infra.Sup;

public sealed class SupPriceQueryGateway : IPriceQueryGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupPriceGatewayOptions _options;
    private readonly ILogger<SupPriceQueryGateway> _logger;

    public SupPriceQueryGateway(
        IHttpClientFactory httpClientFactory,
        IOptions<SupPriceGatewayOptions> options,
        ILogger<SupPriceQueryGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<PriceQueryOutcome> QueryAsync(
        PriceQueryBatch batch,
        CancellationToken cancellationToken)
    {
        return batch.LookupMode == PriceLookupMode.MappedHotel
            ? QueryMappedAsync(batch, cancellationToken)
            : QuerySupplierHotelsAsync(batch, cancellationToken);
    }

    private async Task<PriceQueryOutcome> QueryMappedAsync(
        PriceQueryBatch batch,
        CancellationToken cancellationToken)
    {
        var request = new RoomMatchRequest
        {
            ApiKey = _options.Endpoints.ApiKey,
            Gid = Guid.NewGuid().ToString("N"),
            SupId = batch.SupId.ToString(),
            CheckIn = batch.Combination.Window.CheckIn.ToDateTime(TimeOnly.MinValue),
            CheckOut = batch.Combination.Window.CheckOut.ToDateTime(TimeOnly.MinValue),
            ListMTHotelCode = batch.Tasks.Select(static task => task.MTHotelCode!).ToArray(),
            ListRoom = [CreateRoom(batch.Combination.Adults)],
            TimeOut = _options.Retry.TimeoutMilliseconds
        };

        var response = await SendJsonAsync(
            _options.Endpoints.QueryPriceUrl,
            request,
            _options.Retry.TimeoutMilliseconds,
            cancellationToken);
        if (!response.IsSuccessful)
        {
            return PriceQueryOutcome.Failure(response.Error!);
        }

        var keys = response.Body!.ListPrice
            .Where(static item => !string.IsNullOrWhiteSpace(item.MTHotelCode))
            .Select(static item => item.MTHotelCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hits = batch.Tasks
            .Where(task => task.MTHotelCode is not null && keys.Contains(task.MTHotelCode))
            .Select(static task => task.Id)
            .ToArray();
        return PriceQueryOutcome.Success(hits);
    }

    private async Task<PriceQueryOutcome> QuerySupplierHotelsAsync(
        PriceQueryBatch batch,
        CancellationToken cancellationToken)
    {
        var first = batch.Tasks[0];
        var request = new HotelPriceScreenRequest
        {
            Gid = Guid.NewGuid(),
            MTCurrencyId = 1,
            SupId = batch.SupId,
            SupFlag = first.SupplierFlag ?? string.Empty,
            MTNationalityId = 74,
            CheckInDate = batch.Combination.Window.CheckIn.ToString("yyyy-MM-dd"),
            CheckOutDate = batch.Combination.Window.CheckOut.ToString("yyyy-MM-dd"),
            IsCache = "Y",
            SourceName = "1001",
            ReqSource = "1001",
            TimeOut = _options.Retry.CacheTimeoutMilliseconds,
            ListHotel = batch.Tasks.Select(task => new HotelInfo
            {
                MTHotelCode = task.MTHotelCode ?? string.Empty,
                SContryCode = task.SupplierCountryCode,
                SCityCode = task.SupplierCityCode,
                SHotelCode = task.SHotelCode!
            }).ToList(),
            RoomList = [new HotelRoomList
            {
                AdultNumber = batch.Combination.Adults,
                ListPerson = Enumerable.Range(0, batch.Combination.Adults)
                    .Select(_ => new HotelPerson())
                    .ToList()
            }]
        };

        var response = await SendJsonAsync(
            _options.Endpoints.QueryCachePriceUrl,
            request,
            _options.Retry.CacheTimeoutMilliseconds,
            cancellationToken);
        if (!response.IsSuccessful)
        {
            return PriceQueryOutcome.Failure(response.Error!);
        }

        var keys = response.Body!.ListPrice
            .Select(static item => !string.IsNullOrWhiteSpace(item.SupHotelCode)
                ? item.SupHotelCode
                : item.MTHotelCode)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hits = batch.Tasks
            .Where(task => task.SHotelCode is not null && keys.Contains(task.SHotelCode))
            .Select(static task => task.Id)
            .ToArray();
        return PriceQueryOutcome.Success(hits);
    }

    private async Task<ResponseEnvelope> SendJsonAsync<TRequest>(
        string endpoint,
        TRequest request,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(SupPriceQueryGateway));
        var attempts = _options.Retry.MaxAttempts;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeoutMilliseconds);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(request, JsonOptions),
                        Encoding.UTF8,
                        MediaTypeHeaderValue.Parse("application/json"))
                };
                httpRequest.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

                using var response = await client.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    return ResponseEnvelope.Failure($"Sup returned HTTP {(int)response.StatusCode}.");
                }

                if ((int)response.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Sup returned HTTP {(int)response.StatusCode}.");
                }

                response.EnsureSuccessStatusCode();
                var body = await ReadBodyAsync(response, timeoutCts.Token);
                var parsed = JsonSerializer.Deserialize<HotelPriceScreenResponse>(body, JsonOptions);
                if (parsed is null)
                {
                    throw new JsonException("Sup returned an empty response.");
                }

                if (parsed.Code != 1)
                {
                    return ResponseEnvelope.Failure($"Sup returned business code {parsed.Code}: {parsed.Msg}");
                }

                return ResponseEnvelope.Success(parsed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                if (attempt == attempts)
                {
                    return ResponseEnvelope.Failure($"Sup request timed out after {timeoutMilliseconds} ms.");
                }
            }
            catch (HttpRequestException ex)
            {
                if (attempt == attempts)
                {
                    return ResponseEnvelope.Failure(ex.Message);
                }

                _logger.LogWarning(ex, "Transient Sup request failure on attempt {Attempt}/{Attempts}.", attempt, attempts);
            }
            catch (JsonException ex)
            {
                if (attempt == attempts)
                {
                    return ResponseEnvelope.Failure(ex.Message);
                }

                _logger.LogWarning(ex, "Invalid Sup response on attempt {Attempt}/{Attempts}.", attempt, attempts);
            }
            catch (Exception ex)
            {
                if (attempt == attempts)
                {
                    return ResponseEnvelope.Failure(ex.Message);
                }

                _logger.LogWarning(ex, "Transient Sup request error on attempt {Attempt}/{Attempts}.", attempt, attempts);
            }

            var delay = _options.Retry.InitialDelayMilliseconds * Math.Pow(2, attempt - 1) + Random.Shared.Next(0, 251);
            await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
        }

        return ResponseEnvelope.Failure("Sup request failed after all retry attempts.");
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentEncoding.Any(value =>
                string.Equals(value, "gzip", StringComparison.OrdinalIgnoreCase)))
        {
            await using var compressed = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static RoomRequest CreateRoom(int adults) => new()
    {
        AdultNumber = adults,
        ListPerson = Enumerable.Range(0, adults)
            .Select(_ => new Person())
            .ToList()
    };

    private sealed record ResponseEnvelope(bool IsSuccessful, HotelPriceScreenResponse? Body, string? Error)
    {
        public static ResponseEnvelope Success(HotelPriceScreenResponse body) => new(true, body, null);
        public static ResponseEnvelope Failure(string error) => new(false, null, error);
    }

    private sealed class RoomMatchRequest
    {
        public RoomMatchTypeChoose RoomMatchTypeChoose { get; set; } = new();
        public string ApiKey { get; set; } = string.Empty;
        public string Gid { get; set; } = string.Empty;
        public string SupId { get; set; } = string.Empty;
        public string RequestFrom { get; set; } = "OTANew";
        public string ReqSource { get; set; } = "1001";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public IReadOnlyList<string> ListMTHotelCode { get; set; } = [];
        public List<RoomRequest> ListRoom { get; set; } = [];
        public int MTNationalityId { get; set; } = 74;
        public int TimeOut { get; set; }
    }

    private sealed class RoomMatchTypeChoose
    {
        public string RoomSource { get; set; } = "115";
        public string IsMustAuditRoom { get; set; } = "N";
    }

    private sealed class RoomRequest
    {
        public int ChildNumber { get; set; }
        public int AdultNumber { get; set; }
        public List<Person> ListPerson { get; set; } = [];
    }

    private sealed class Person
    {
        public int RoomIndex { get; set; } = 1;
        public int Type { get; set; } = 1;
        public int Age { get; set; } = 30;
        public string FirstName { get; set; } = "TBA";
        public string LastName { get; set; } = "TBA";
    }

    private sealed class HotelPriceScreenRequest
    {
        public Guid Gid { get; set; }
        public int MTCurrencyId { get; set; }
        public int SupId { get; set; }
        public string SupFlag { get; set; } = string.Empty;
        public int MTNationalityId { get; set; } = 74;
        public List<HotelInfo> ListHotel { get; set; } = [];
        public string CheckInDate { get; set; } = string.Empty;
        public string CheckOutDate { get; set; } = string.Empty;
        public int RoomNumber { get; set; } = 1;
        public List<HotelRoomList> RoomList { get; set; } = [];
        public string IsCache { get; set; } = "Y";
        public int TimeOut { get; set; }
        public string SourceName { get; set; } = "1001";
        public string ReqSource { get; set; } = "1001";
    }

    private sealed class HotelInfo
    {
        public string MTHotelCode { get; set; } = string.Empty;
        public string? SContryCode { get; set; }
        public string? SCityCode { get; set; }
        public string SHotelCode { get; set; } = string.Empty;
    }

    private sealed class HotelRoomList
    {
        public int ChildNumber { get; set; }
        public int AdultNumber { get; set; }
        public List<HotelPerson> ListPerson { get; set; } = [];
    }

    private sealed class HotelPerson
    {
        public int Age { get; set; } = 30;
        public int Type { get; set; } = 1;
        public int RoomIndex { get; set; } = 1;
        public string FirstName { get; set; } = "AA";
        public string LastName { get; set; } = "BB";
    }

    private sealed class HotelPriceScreenResponse
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Code { get; set; }
        public string? Msg { get; set; }
        public List<PriceResult> ListPrice { get; set; } = [];
    }

    private sealed class PriceResult
    {
        public string? MTHotelCode { get; set; }
        public string? SupHotelCode { get; set; }
    }
}

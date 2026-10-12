using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.HTTP.OTAHotelInfo.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.HTTP.OTAHotelInfo;

public sealed class OtaHotelInfoClient : IOtaHotelInfoClient
{
    static readonly DateTime IdQueryStartTime = new(1970, 1, 1);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    static OtaHotelInfoClient()
    {
        JsonOptions.Converters.Add(new FlexibleNullableDateTimeConverter());
    }

    readonly HttpClient _httpClient;
    readonly IOptionsMonitor<OtaHotelInfoOptions> _options;

    public OtaHotelInfoClient(
        HttpClient httpClient,
        IOptionsMonitor<OtaHotelInfoOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public Task<PageResult<BaseHotelInfo>> QueryByMinHotelIdAsync(
        int minHotelId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (minHotelId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minHotelId), "Minimum hotel ID cannot be negative.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        return QueryByMinHotelIdPageAsync(minHotelId, pageSize, 1, cancellationToken);
    }

    public Task<PageResult<BaseHotelInfo>> QueryByMinHotelIdPageAsync(
        int minHotelId,
        int pageSize,
        int pageIndex,
        CancellationToken cancellationToken = default)
    {
        if (minHotelId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minHotelId), "Minimum hotel ID cannot be negative.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        if (pageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index must be greater than zero.");
        }

        return QueryAsync(
            new HotelInfoPageReq
            {
                MinHotelId = minHotelId,
                DateTime = IdQueryStartTime,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            cancellationToken);
    }

    public Task<PageResult<BaseHotelInfo>> QueryByTimeAsync(
        DateTime startTime,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        if (endTime < startTime)
        {
            throw new ArgumentOutOfRangeException(nameof(endTime), "End time cannot be earlier than start time.");
        }

        return QueryAsync(
            new HotelInfoPageReq
            {
                DateTime = startTime,
                EndTime = endTime,
                PageIndex = 1,
                PageSize = int.MaxValue
            },
            cancellationToken);
    }

    async Task<PageResult<BaseHotelInfo>> QueryAsync(
        HotelInfoPageReq request,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var requestUri = BuildRequestUri(options);
        var json = JsonSerializer.Serialize(request, JsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));

        try
        {
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var result = await JsonSerializer.DeserializeAsync<OtaHotelInfoResponse<PageResult<BaseHotelInfo>>>(
                stream,
                JsonOptions,
                timeoutCts.Token);

            if (result is null)
            {
                throw new InvalidDataException("OTA hotel information API returned an empty JSON response.");
            }

            if (result.ResultCode != 0)
            {
                throw new OtaHotelInfoApiException(result.ResultCode, result.ResultMsg);
            }

            return result.Data
                ?? throw new InvalidDataException("OTA hotel information API returned a successful result without data.");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"OTA hotel information query timed out after {Math.Max(1, options.TimeoutSeconds)} seconds.",
                ex);
        }
    }

    static Uri BuildRequestUri(OtaHotelInfoOptions options)
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("OTA hotel information endpoint must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            throw new InvalidOperationException("OTA hotel information user name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Md5Key))
        {
            throw new InvalidOperationException("OTA hotel information MD5 key is required.");
        }

        var builder = new UriBuilder(endpoint);
        var existingQuery = builder.Query.TrimStart('?');
        var authenticationQuery = $"userName={Uri.EscapeDataString(options.UserName)}&Md5Key={Uri.EscapeDataString(options.Md5Key)}";
        builder.Query = string.IsNullOrEmpty(existingQuery)
            ? authenticationQuery
            : $"{existingQuery}&{authenticationQuery}";
        return builder.Uri;
    }

    sealed class OtaHotelInfoResponse<T>
    {
        public T? Data { get; set; }

        public int ResultCode { get; set; }

        public string? ResultMsg { get; set; }
    }
}

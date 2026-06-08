using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Infrastructure.HTTP.QueryPrice.Sup.Models;
using Microsoft.Extensions.Logging;
using QueryPriceSupRoomBuilder;

namespace Infrastructure.HTTP.QueryPrice.Sup;

public sealed class SupQueryPriceApi : IQueryPriceClient
{
#if DEBUG
    static readonly Uri QueryManySupHotelPriceUri = new("http://47.110.155.251:7000/SupApi/Hotel/QueryManySupHotelPrice");
#else
    static readonly Uri QueryManySupHotelPriceUri = new("http://sup.nginx.m-tu.com:7000/SupApi/Hotel/QueryManySupHotelPrice");
#endif
    const string ApiKey = "c9a9d73755ec58746c08746e0d36eb26";

    readonly IHttpClientFactory _httpClientFactory;
    readonly SupQueryPriceMetrics _metrics;
    readonly ILogger<SupQueryPriceApi> _logger;

    public SupQueryPriceApi(
        IHttpClientFactory httpClientFactory,
        SupQueryPriceMetrics metrics,
        ILogger<SupQueryPriceApi> logger)
    {
        _httpClientFactory = httpClientFactory;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<QueryPriceResponse?> QueryPriceAsync(QueryPriceRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = "error";
        _metrics.RecordRequest(request.SupId, request.ListMTHotelCode.Count);

        try
        {
            using var httpRequest = CreateJsonPostRequest(QueryManySupHotelPriceUri, CreateRoomMatchRequest(request));
            using var response = await SendAsync(httpRequest, 120000, cancellationToken);
            var responseBody = await ReadResponseBodyAsync(response, cancellationToken);
            var result = JsonSerializer.Deserialize<HotelPriceScreenResponse>(responseBody, QueryPriceJson.Options);
            if (!IsSuccessCode(result?.Code))
            {
                outcome = "error_code";
                _metrics.RecordErrorCode(request.SupId, request.ListMTHotelCode.Count, result?.Code.ToString());
            }
            else
            {
                outcome = result?.ListPrice.Count > 0 ? "priced" : "no_price";
            }

            _logger.LogTrace(
                "{HotelCodes} {CheckIn} - {CheckOut} {Outcome} code:{Code} msg:{Message}",
                string.Join(',', request.ListMTHotelCode),
                request.CheckIn,
                request.CheckOut,
                outcome,
                result?.Code,
                result?.Msg);

            return result is null
                ? null
                : new QueryPriceResponse(result.Code, result.Msg, result.ListPrice.Select(Map).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            outcome = "timeout";
            _logger.LogWarning(ex, "Query price timed out. SupId={SupId}, Hotels={Hotels}", request.SupId, string.Join(',', request.ListMTHotelCode));
            return null;
        }
        catch (Exception ex)
        {
            outcome = "error";
            _logger.LogError(ex, "Query price failed. SupId={SupId}, Hotels={Hotels}", request.SupId, string.Join(',', request.ListMTHotelCode));
            return null;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordResult(request.SupId, request.ListMTHotelCode.Count, outcome);
            _metrics.RecordDuration(request.SupId, request.ListMTHotelCode.Count, outcome, stopwatch.Elapsed);
        }
    }

    async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMilliseconds));

        var client = _httpClientFactory.CreateClient(nameof(SupQueryPriceApi));
        try
        {
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"HTTP request to {request.RequestUri} timed out after {timeoutMilliseconds} ms.", ex);
        }
    }

    static HttpRequestMessage CreateJsonPostRequest<T>(Uri uri, T request)
    {
        var json = JsonSerializer.Serialize(request, QueryPriceJson.Options);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"))
        };

        httpRequest.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        return httpRequest;
    }

    static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentEncoding.Any(x => string.Equals(x, "gzip", StringComparison.OrdinalIgnoreCase)))
        {
            await using var compressedStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var decompressedStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressedStream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    static RoomMatchRequest CreateRoomMatchRequest(QueryPriceRequest input)
    {
        var persons = new List<ListPerson>();
        const int roomIndex = 1;

        for (var i = 0; i < input.AdultNumber; i++)
        {
            persons.Add(new ListPerson { RoomIndex = roomIndex, Type = 1, Age = 30 });
        }

        for (var i = 0; i < input.ChildNumber; i++)
        {
            persons.Add(new ListPerson { RoomIndex = roomIndex, Type = 2, Age = 6 });
        }

        return new RoomMatchRequest
        {
            RoomMatchTypeChoose = new RoomMatchTypeChoose
            {
                RoomSource = "115",
                IsMustAuditRoom = "N"
            },
            ApiKey = ApiKey,
            Gid = Guid.NewGuid().ToString("N"),
            SupId = input.SupId,
            RequestFrom = "OTANew",
            ReqSource = "1001",
            CheckIn = input.CheckIn,
            CheckOut = input.CheckOut,
            ListMTHotelCode = input.ListMTHotelCode,
            ListRoom =
            [
                new ListRoom
                {
                    ChildNumber = input.ChildNumber,
                    AdultNumber = input.AdultNumber,
                    ListPerson = persons
                }
            ],
            MTNationalityId = input.MTNationalityId,
            TimeOut = 120000
        };
    }

    static QueryPriceRoom Map(PriceResult price)
    {
        return new QueryPriceRoom
        {
            SupId = price.SupId,
            MTHotelCode = price.MTHotelCode,
            SupplierName = price.SupplierName,
            SupRoomName = price.SupRoomName,
            SupRoomEnName = price.SupRoomEnName,
            SupRoomCode = price.SupRoomCode,
            SupHotelCode = price.SupHotelCode,
            SupBedInfo = price.SupBedInfo,
            MTBeds = price.MTBeds,
            BedInfoKey = price.BedInfoKey,
            Person = price.Person,
            CheckIn = price.CheckIn,
            CheckOut = price.CheckOut,
            SupTotalPrice = price.SupTotalPrice,
            SupTotalPriceRMB = price.SupTotalPriceRMB
        };
    }

    static bool IsSuccessCode(int? code)
    {
        return code is null or 0 or 1;
    }
}

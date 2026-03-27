using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapClient : IExpediaTaapClient
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ExpediaTaapRequestRateLimiter _requestRateLimiter;
    private readonly IOptionsMonitor<ExpediaTaapOptions> _optionsMonitor;

    public ExpediaTaapClient(
        HttpClient httpClient,
        ExpediaTaapRequestRateLimiter requestRateLimiter,
        IOptionsMonitor<ExpediaTaapOptions> optionsMonitor)
    {
        _httpClient = httpClient;
        _requestRateLimiter = requestRateLimiter;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<ExpediaTaapPriceQueryResult> QueryPricesAsync(
        int accountId,
        ExpediaTaapLoginState loginState,
        ExpediaTaapPriceQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var options = _optionsMonitor.CurrentValue;
        var requestContent = BuildRequestContent(request, loginState, options);
        using var response = await SendAsync(accountId, requestContent, loginState, options, cancellationToken);
        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (HasPersistedQueryNotFound(rawContent) && string.IsNullOrWhiteSpace(request.Sha256Hash))
        {
            throw new ExpediaTaapLoginStateExpiredException(
                "Expedia TAAP 当前共享 sha256Hash 已失效。",
                response.StatusCode,
                rawContent,
                shouldRefreshSharedSha256Hash: true);
        }

        if (ShouldRefreshLoginState(response.StatusCode))
        {
            throw new ExpediaTaapLoginStateExpiredException(
                $"Expedia TAAP 请求返回状态码 {(int)response.StatusCode}，当前登录态已失效。",
                response.StatusCode,
                rawContent);
        }

        if (HasPersistedQueryNotFound(rawContent))
        {
            throw new InvalidOperationException("传入的 Expedia TAAP sha256Hash 无效。");
        }

        return BuildResult(response, request, requestContent, rawContent);
    }

    private static ExpediaTaapPriceQueryResult BuildResult(
        HttpResponseMessage response,
        ExpediaTaapPriceQueryRequest request,
        string requestContent,
        string rawContent)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Expedia TAAP 请求失败，状态码 {(int)response.StatusCode}，响应内容：{rawContent}",
                null,
                response.StatusCode);
        }

        return new ExpediaTaapPriceQueryResult
        {
            PropertyId = request.PropertyId,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            RawContent = rawContent,
            RequestContent = requestContent
        };
    }

    private static bool ShouldRefreshLoginState(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
    }

    private static bool HasPersistedQueryNotFound(string rawContent)
    {
        return rawContent.Contains("PERSISTED_QUERY_NOT_FOUND", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> SendAsync(
        int accountId,
        string requestContent,
        ExpediaTaapLoginState loginState,
        ExpediaTaapOptions options,
        CancellationToken cancellationToken)
    {
        using var lease = await _requestRateLimiter.WaitAsync(accountId, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException($"账号 {accountId} 的 Expedia TAAP 请求被限流器拒绝。");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, options.GraphQlPath)
        {
            Content = new StringContent(requestContent, Encoding.UTF8, "application/json")
        };

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("Cookie", loginState.CookieHeader);
        message.Headers.TryAddWithoutValidation("Client-Info", $"shopping-pwa,{loginState.ClientInfoToken},{options.GraphQlRegion}");
        return await _httpClient.SendAsync(message, cancellationToken);
    }

    private static void ValidateRequest(ExpediaTaapPriceQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PropertyId))
        {
            throw new ArgumentException("PropertyId 不能为空。", nameof(request));
        }

        if (request.CheckOut <= request.CheckIn)
        {
            throw new ArgumentException("CheckOut 必须晚于 CheckIn。", nameof(request));
        }

        if (request.Rooms <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Rooms), "Rooms 必须大于 0。");
        }

        if (request.Adults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Adults), "Adults 必须大于 0。");
        }
    }

    private static string BuildRequestContent(
        ExpediaTaapPriceQueryRequest request,
        ExpediaTaapLoginState loginState,
        ExpediaTaapOptions options)
    {
        var occupancies = BuildRoomOccupancies(request);
        var payload = new[]
        {
            new
            {
                OperationName = "RoomsAndRatesPropertyOffersQuery",
                Extensions = new
                {
                    PersistedQuery = new
                    {
                        Version = 1,
                        Sha256Hash = string.IsNullOrWhiteSpace(request.Sha256Hash)
                            ? loginState.Sha256Hash
                            : request.Sha256Hash
                    }
                },
                Variables = new
                {
                    Context = new
                    {
                        Locale = string.IsNullOrWhiteSpace(request.Locale) ? options.Locale : request.Locale,
                        Currency = string.IsNullOrWhiteSpace(request.Currency) ? options.Currency : request.Currency,
                        DebugContext = new { AbacusOverrides = Array.Empty<int>() },
                        Device = new { Type = options.DeviceType },
                        Eapid = loginState.Eapid,
                        Identity = new
                        {
                            AuthState = options.AuthState,
                            Duaid = loginState.Duaid
                        },
                        PrivacyTrackingState = options.PrivacyTrackingState,
                        SiteId = options.SiteId
                    },
                    Referrer = options.Referrer,
                    PropertyId = request.PropertyId,
                    SearchCriteria = new
                    {
                        Primary = new
                        {
                            DateRange = new
                            {
                                CheckInDate = new
                                {
                                    Day = request.CheckIn.Day,
                                    Month = request.CheckIn.Month,
                                    Year = request.CheckIn.Year
                                },
                                CheckOutDate = new
                                {
                                    Day = request.CheckOut.Day,
                                    Month = request.CheckOut.Month,
                                    Year = request.CheckOut.Year
                                }
                            },
                            Destination = new
                            {
                                Coordinates = new { Latitude = 0.0, Longitude = 0.0 },
                                MapBounds = (string?)null,
                                PinnedPropertyId = (int?)null,
                                PropertyIds = (int?)null,
                                RegionId = (string?)null,
                                RegionName = (string?)null
                            },
                            Rooms = occupancies
                        },
                        Secondary = new
                        {
                            Booleans = new[]
                            {
                                new
                                {
                                    Id = "showPackageRates",
                                    Value = options.ShowPackageRates
                                }
                            },
                            Counts = Array.Empty<int>(),
                            Ranges = Array.Empty<int>(),
                            Selections = new[]
                            {
                                new { Id = "privacyTrackingState", Value = options.PrivacyTrackingState },
                                new { Id = "searchId", Value = Guid.NewGuid().ToString() },
                                new { Id = "sort", Value = options.Sort },
                                new { Id = "useRewards", Value = options.UseRewards }
                            }
                        }
                    },
                    SearchOffer = (object?)null,
                    ShoppingContext = new { MultiItem = (string?)null },
                    TravelAdTrackingInfo = (object?)null
                }
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static object[] BuildRoomOccupancies(ExpediaTaapPriceQueryRequest request)
    {
        if (request.RoomOccupancies is { Count: > 0 })
        {
            return request.RoomOccupancies
                .Select(room => new
                {
                    Adults = room.Adults,
                    Children = room.ChildrenAges.Select(age => new { Age = age }).ToArray()
                })
                .Cast<object>()
                .ToArray();
        }

        return Enumerable.Range(0, request.Rooms)
            .Select(_ => new
            {
                Adults = request.Adults,
                Children = Array.Empty<object>()
            })
            .Cast<object>()
            .ToArray();
    }
}

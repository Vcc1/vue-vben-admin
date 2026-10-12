using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.IO.Compression;
using HotelPriceAvailability.App.Contracts;
using HotelPriceAvailability.Domain;
using HotelPriceAvailability.Infra.Sup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http;
using Xunit;

namespace HotelPriceAvailability.Infra.Tests;

public sealed class SupPriceQueryGatewayTests
{
    [Fact]
    public async Task QueryAsync_MapsSupplierHotelCacheResultToTaskId()
    {
        var factory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"code\":1,\"listPrice\":[{\"supHotelCode\":\"SUP-1\"}]}", Encoding.UTF8, "application/json")
        });
        var gateway = CreateGateway(factory);
        var batch = CreateBatch(PriceLookupMode.SupplierHotel, "SUP-1");

        var result = await gateway.QueryAsync(batch, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal([7], result.HitTaskIds);
    }

    [Fact]
    public async Task QueryAsync_MapsMappedHotelResultToTaskId()
    {
        var factory = new TestHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"code\":1,\"listPrice\":[{\"mtHotelCode\":\"MT-1\"}]}", Encoding.UTF8, "application/json")
        });
        var gateway = CreateGateway(factory);
        var batch = CreateBatch(PriceLookupMode.MappedHotel, "MT-1");

        var result = await gateway.QueryAsync(batch, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal([7], result.HitTaskIds);
    }

    [Fact]
    public async Task QueryAsync_RetriesServerErrorAndReadsGzipResponse()
    {
        var calls = 0;
        var factory = new TestHttpClientFactory(_ =>
        {
            calls++;
            if (calls == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            var payload = Encoding.UTF8.GetBytes("{\"code\":1,\"listPrice\":[{\"mtHotelCode\":\"MT-1\"}]}");
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(payload);
            }

            var content = new ByteArrayContent(output.ToArray());
            content.Headers.ContentEncoding.Add("gzip");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var gateway = CreateGateway(factory, maxAttempts: 2);

        var result = await gateway.QueryAsync(CreateBatch(PriceLookupMode.MappedHotel, "MT-1"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, calls);
        Assert.Equal([7], result.HitTaskIds);
    }

    private static SupPriceQueryGateway CreateGateway(TestHttpClientFactory factory, int maxAttempts = 1) => new(
        factory,
        Options.Create(new SupPriceGatewayOptions
        {
            Endpoints = new SupPriceEndpoints
            {
                QueryPriceUrl = "http://test/query",
                QueryCachePriceUrl = "http://test/cache",
                ApiKey = "test-key"
            },
            Retry = new SupPriceRetryOptions
            {
                MaxAttempts = maxAttempts,
                InitialDelayMilliseconds = 0,
                TimeoutMilliseconds = 5000,
                CacheTimeoutMilliseconds = 5000
            }
        }),
        NullLogger<SupPriceQueryGateway>.Instance);

    private static PriceQueryBatch CreateBatch(PriceLookupMode mode, string code)
    {
        var task = mode == PriceLookupMode.MappedHotel
            ? new PriceCheckTask(7, 51, code, null, null, null, null)
            : new PriceCheckTask(7, 51, null, code, "flag", "CN", "SHA");
        var combination = new PriceSearchCombination(
            new PriceSearchWindow(new DateOnly(2026, 7, 22), new DateOnly(2026, 7, 23), 0, 0),
            2,
            0,
            0);
        return new PriceQueryBatch(51, mode, [task], combination);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(new DelegateHandler(_handler));

        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(_handler(request));
        }
    }
}

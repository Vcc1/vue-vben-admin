using System.Diagnostics;
using ApiGateway.App;
using ApiGateway.Domain;
using Infrastructure.Sql.Admin;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Infra;

internal sealed class GatewayProxyService(
    AdminDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<GatewayProxyService> logger) : IGatewayProxyService
{
    public async Task<GatewayProxyResult> ProxyRequestAsync(GatewayProxyContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Build upstream URL: BaseUrl + remainingPath (which includes module prefix + endpoint path)
        var upstreamUrl = context.BaseUrl.TrimEnd('/') + "/" + context.UpstreamPath.TrimStart('/');
        if (context.IncomingRequest.QueryString.HasValue
            && context.IncomingRequest.QueryString.Value!.Length > 1)
        {
            upstreamUrl += context.IncomingRequest.QueryString.Value;
        }

        var client = httpClientFactory.CreateClient("Gateway");
        using var requestMessage = new HttpRequestMessage(new HttpMethod(context.HttpMethod), upstreamUrl);

        // Copy request body
        if (context.HttpMethod is not ("GET" or "HEAD" or "DELETE" or "TRACE"))
        {
            var bodyStream = new MemoryStream();
            await context.IncomingRequest.Body.CopyToAsync(bodyStream, ct);
            if (bodyStream.Length > 0)
            {
                bodyStream.Position = 0;
                requestMessage.Content = new StreamContent(bodyStream);
            }
        }

        // Copy incoming headers (skip Host)
        foreach (var header in context.IncomingRequest.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        // Inject merged custom headers from API + Module config
        foreach (var header in context.MergedHeaders)
        {
            requestMessage.Headers.Remove(header.Key);
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        requestMessage.Headers.Remove("User-Agent");
        requestMessage.Headers.TryAddWithoutValidation("User-Agent", "MengtuV-Gateway/1.0");

        // Forward
        using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
        var responseBody = await response.Content.ReadAsByteArrayAsync(ct);
        sw.Stop();

        // Collect response headers
        var responseHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in response.Headers) responseHeaders[h.Key] = h.Value.ToArray();
        foreach (var h in response.Content.Headers) responseHeaders[h.Key] = h.Value.ToArray();

        var contentType = response.Content.Headers.ContentType?.ToString();

        // Audit log
        db.GatewayAuditLogs.Add(new GatewayAuditLog
        {
            ApiId = context.ApiId,
            ApiName = context.ApiName,
            ModuleId = context.ModuleId,
            ModuleName = context.ModuleName,
            EndpointId = context.EndpointId,
            ActorType = context.ActorType,
            ActorId = context.ActorId,
            ActorName = context.ActorName,
            AuthenticationType = context.AuthenticationType,
            HttpMethod = context.HttpMethod,
            Path = "/" + context.UpstreamPath.TrimStart('/'),
            StatusCode = (int)response.StatusCode,
            DurationMs = sw.ElapsedMilliseconds,
            OccurredAtUtc = DateTime.UtcNow,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent
        });
        await db.SaveChangesAsync(ct);

        return new GatewayProxyResult(
            (int)response.StatusCode, responseBody, contentType, responseHeaders, sw.ElapsedMilliseconds);
    }
}

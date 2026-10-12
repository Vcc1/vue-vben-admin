using System.Security.Claims;
using ApiGateway.App;
using ApiGateway.Domain;
using Infrastructure.Sql.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Infra;

internal sealed class GatewayProxyMiddleware(
    RequestDelegate next,
    ILogger<GatewayProxyMiddleware> logger)
{
    private const string GatewayPrefix = "/gateway/";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith(GatewayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var afterPrefix = path[GatewayPrefix.Length..];
        var slashIdx = afterPrefix.IndexOf('/');
        var apiKey = slashIdx > 0 ? afterPrefix[..slashIdx] : afterPrefix;
        var remainingPath = slashIdx > 0 ? afterPrefix[slashIdx..] : "/";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required." });
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var scope = context.RequestServices.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var proxyService = scope.ServiceProvider.GetRequiredService<IGatewayProxyService>();

            // 1. Find API by ApiKey (Include Modules → Endpoints)
            var api = await db.GatewayApis.AsNoTracking()
                .SingleOrDefaultAsync(a => a.ApiKey == apiKey && a.IsEnabled, context.RequestAborted);

            if (api is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { error = $"Gateway API '{apiKey}' not found." });
                return;
            }

            var modules = await db.GatewayModules.AsNoTracking()
                .Where(m => m.ApiId == api.Id && m.IsEnabled)
                .OrderByDescending(m => m.PathPrefix.Length) // Longest prefix first
                .ToListAsync(context.RequestAborted);

            // 2. Find matching Module by PathPrefix
            GatewayModule? matchedModule = null;
            string endpointPath = remainingPath;
            foreach (var module in modules)
            {
                var prefix = module.PathPrefix.TrimEnd('/');
                if (remainingPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchedModule = module;
                    endpointPath = remainingPath[prefix.Length..];
                    if (string.IsNullOrEmpty(endpointPath)) endpointPath = "/";
                    break;
                }
            }

            if (matchedModule is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { error = $"No module matched for path '{remainingPath}'." });
                return;
            }

            // 3. Find matching Endpoint (optional — for audit precision)
            var httpMethod = context.Request.Method.ToUpperInvariant();
            var endpoints = await db.GatewayEndpoints.AsNoTracking()
                .Where(e => e.ModuleId == matchedModule.Id && e.IsEnabled)
                .ToListAsync(context.RequestAborted);

            GatewayEndpoint? matchedEndpoint = null;
            foreach (var ep in endpoints)
            {
                if (ep.HttpMethod.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)
                    && GatewayPermissionMatcher.PathMatches(ep.Path, endpointPath))
                {
                    matchedEndpoint = ep;
                    break;
                }
            }

            // 4. Permission check
            var actorType = context.User.Identity.AuthenticationType ?? "Unknown";
            var actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var actorName = context.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            var permissions = await db.GatewayPermissions.AsNoTracking()
                .Where(p => roles.Contains(p.RoleName) && p.ModuleId == matchedModule.Id && p.IsEnabled)
                .ToListAsync(context.RequestAborted);

            var hasPermission = permissions.Any(p =>
                p.EndpointId is null // Module-level wildcard
                || (matchedEndpoint is not null && p.EndpointId == matchedEndpoint.Id));

            if (!hasPermission)
            {
                logger.LogWarning("Gateway access denied: actor={Actor} roles={Roles} api={Api} module={Module} method={Method} path={Path}",
                    actorName, string.Join(",", roles), apiKey, matchedModule.Name, httpMethod, remainingPath);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Access denied." });
                return;
            }

            // 5. Merge headers
            var mergedHeaders = HeaderHelper.MergeHeaders(
                HeaderHelper.ParseHeaders(api.HeadersJson),
                HeaderHelper.ParseHeaders(matchedModule.HeadersJson));

            // 6. Build proxy context
            var proxyContext = new GatewayProxyContext(
                api.Id, api.Name, api.ApiKey, api.BaseUrl,
                matchedModule.Id, matchedModule.Name, matchedModule.PathPrefix,
                matchedEndpoint?.Id,
                httpMethod, remainingPath, mergedHeaders,
                actorType, actorId, actorName, actorType,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                context.Request);

            // 7. Execute proxy
            var result = await proxyService.ProxyRequestAsync(proxyContext, context.RequestAborted);

            // 8. Write response
            context.Response.StatusCode = result.StatusCode;
            foreach (var header in result.ResponseHeaders)
            {
                if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                context.Response.Headers[header.Key] = header.Value;
            }
            if (result.ContentType is not null)
                context.Response.ContentType = result.ContentType;
            if (result.Body.Length > 0)
                await context.Response.Body.WriteAsync(result.Body, context.RequestAborted);
        }
        finally
        {
            if (scope is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                scope.Dispose();
        }
    }
}

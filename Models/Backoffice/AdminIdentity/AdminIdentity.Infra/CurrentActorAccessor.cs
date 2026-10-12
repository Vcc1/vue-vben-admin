using System.Security.Claims;
using AdminIdentity.App;
using Microsoft.AspNetCore.Http;

namespace AdminIdentity.Infra;

internal sealed class CurrentActorAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentActorAccessor
{
    public CurrentActor Current
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            var principal = context?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return CurrentActor.System;
            }

            var authenticationType = principal.Identity.AuthenticationType ?? "Unknown";
            var actorType = string.Equals(authenticationType, ApiKeyAuthenticationHandler.SchemeName, StringComparison.Ordinal)
                ? "ApiKey"
                : "User";
            var actorId = actorType == "ApiKey"
                ? principal.FindFirstValue("api_key_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"
                : principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

            return new CurrentActor(
                actorType,
                actorId,
                principal.FindFirstValue(ClaimTypes.Name) ?? principal.Identity.Name ?? actorId,
                authenticationType,
                context?.Connection.RemoteIpAddress?.ToString(),
                context?.Request.Headers.UserAgent.ToString(),
                context?.TraceIdentifier);
        }
    }
}

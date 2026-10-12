using System.Security.Claims;
using AdminIdentity.App;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace OidcServer.Api.Controllers;

public sealed class EndSessionController(
    IConfiguration configuration,
    ISessionService sessions) : Controller
{
    [HttpGet("~/connect/endsession")]
    [HttpPost("~/connect/endsession")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> HandleEndSessionAsync(CancellationToken cancellationToken)
    {
        // RP-Initiated Logout：先吊销 sid（会向所有已签发过 token 的客户端推 logout_token），再清本地 cookie。
        var sid = User.FindFirstValue(SessionClaims.Sid);
        if (sid is null)
        {
            var authenticated = await HttpContext.AuthenticateAsync();
            sid = authenticated.Principal?.FindFirstValue(SessionClaims.Sid);
        }

        if (sid is not null)
        {
            await sessions.RevokeAsync(sid, "rp-initiated-logout", cancellationToken);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        var postLogoutRedirectUri = Request.Query["post_logout_redirect_uri"].FirstOrDefault();
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            var allowedUris = configuration.GetSection("OidcServer:AllowedPostLogoutRedirectUris")
                .Get<string[]>() ?? [];

            if (allowedUris.Any(u => postLogoutRedirectUri.StartsWith(u, StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect(postLogoutRedirectUri);
            }
        }

        return Redirect("~/");
    }
}

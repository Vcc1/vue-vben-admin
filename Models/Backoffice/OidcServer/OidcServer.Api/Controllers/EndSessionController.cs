using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace OidcServer.Api.Controllers;

public sealed class EndSessionController(
    IConfiguration configuration) : Controller
{
    [HttpGet("~/connect/endsession")]
    [HttpPost("~/connect/endsession")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> HandleEndSessionAsync()
    {
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

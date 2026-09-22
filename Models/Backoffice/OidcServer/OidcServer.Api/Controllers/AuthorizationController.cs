using System.Security.Claims;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcServer.Api.Controllers;

public sealed class AuthorizationController(
    UserManager<AdminUser> userManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> HandleAuthorizeAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Retrieve the user principal from the authentication middleware.
        var result = await HttpContext.AuthenticateAsync();
        var principal = result.Principal;

        // If the user is not authenticated, redirect to the admin login page.
        if (principal is null || !principal.Identity?.IsAuthenticated == true)
        {
            var returnUrl = Request.Path + Request.QueryString;
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = returnUrl
            });
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(Claims.Subject)
            ?? string.Empty;

        var identity = new ClaimsIdentity(
            principal.Identity!.AuthenticationType,
            Claims.Name, Claims.Role);

        var claimTypeMap = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = Claims.Subject,
            [ClaimTypes.Name] = Claims.PreferredUsername,
            [ClaimTypes.Email] = Claims.Email,
            [ClaimTypes.Role] = Claims.Role,
            [ClaimTypes.GivenName] = Claims.GivenName,
        };

        foreach (var claim in principal.Claims)
        {
            if (claimTypeMap.TryGetValue(claim.Type, out var mappedType))
            {
                identity.AddClaim(new Claim(mappedType, claim.Value));
            }
            else if (claim.Type is "display_name")
            {
                identity.AddClaim(new Claim("display_name", claim.Value));
            }
        }

        if (!identity.HasClaim(c => c.Type == Claims.Subject))
        {
            identity.AddClaim(new Claim(Claims.Subject, userId));
        }

        // Enrich claims from database for claims not present in the cookie.
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is not null)
            {
                if (!identity.HasClaim(c => c.Type == Claims.Email)
                    && !string.IsNullOrEmpty(user.Email))
                {
                    identity.AddClaim(new Claim(Claims.Email, user.Email));
                }

                if (!identity.HasClaim(c => c.Type == "display_name")
                    && !string.IsNullOrEmpty(user.DisplayName))
                {
                    identity.AddClaim(new Claim("display_name", user.DisplayName));
                }

                // Cookie principal from AddIdentityCore does not carry role claims;
                // load them here so the issued access token includes `role`.
                var roles = await userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    if (!identity.HasClaim(c => c.Type == Claims.Role && c.Value == role))
                    {
                        identity.AddClaim(new Claim(Claims.Role, role));
                    }
                }
            }
        }

        foreach (var scope in request.GetScopes())
        {
            identity.AddClaim(new Claim(Claims.Scope, scope));
        }

        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Set the authorization response properties.
        claimsPrincipal.SetScopes(request.GetScopes());

        // OpenIddict strips principal claims from the access token unless a
        // destination is set. The resource server (PayCardHub.Api) and
        // /connect/userinfo both read `role` from the access token, so expose
        // every claim except scope (OpenIddict manages scope separately).
        foreach (var claim in claimsPrincipal.Claims)
        {
            if (claim.Type == Claims.Scope)
            {
                continue;
            }

            claim.SetDestinations(Destinations.AccessToken);
        }

        // Return the sign-in result to let OpenIddict issue the authorization code.
        return SignIn(claimsPrincipal, "OpenIddict.Server.AspNetCore");
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcServer.Api.Controllers;

public sealed class AuthorizationController : Controller
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

        // At this point the user is authenticated. Check if we need consent.
        // For systematic consent, we always ask. For simple consent, check if already granted.
        // In passthrough mode, we need to create a ClaimsIdentity and sign in to issue the authorization code.

        var userId = principal.FindFirstValue(Claims.Subject)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;

        // Build the claims identity for the authorization response.
        var identity = new ClaimsIdentity(
            principal.Identity!.AuthenticationType,
            Claims.Name, Claims.Role);

        // Copy essential claims.
        foreach (var claim in principal.Claims)
        {
            identity.AddClaim(claim);
        }

        // Ensure the subject claim is present.
        identity.AddClaim(new Claim(Claims.Subject, userId));

        // Set scopes as claims based on the requested scopes.
        foreach (var scope in request.GetScopes())
        {
            identity.AddClaim(new Claim(Claims.Scope, scope));
        }

        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Set the authorization response properties.
        claimsPrincipal.SetScopes(request.GetScopes());

        // Return the sign-in result to let OpenIddict issue the authorization code.
        return SignIn(claimsPrincipal, "OpenIddict.Server.AspNetCore");
    }
}

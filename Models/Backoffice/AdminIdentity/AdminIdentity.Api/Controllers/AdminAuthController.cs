using AdminIdentity.App;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/admin/auth")]
public sealed class AdminAuthController(
    IAdminAuthenticationService authenticationService,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenPair>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(
            request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AdminUserItem>> MeAsync(CancellationToken cancellationToken)
    {
        var user = await authenticationService.GetCurrentUserAsync(cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize]
    [HttpGet("antiforgery")]
    public IActionResult AntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}

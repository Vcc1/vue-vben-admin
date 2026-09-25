using System.Security.Claims;
using AdminIdentity.App;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Route("api/admin/auth")]
public sealed class AdminAuthController(
    IAdminAuthenticationService authenticationService,
    IAdminUserService userService,
    ISessionService sessionService,
    IStepUpService stepUpService,
    ICurrentActorAccessor actorAccessor,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }
        // 密码已通过但设备不受信：428 告知前端必须先完成二次验证，响应体携带可用通道。
        if (result.StepUp is not null)
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, result);
        }
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("stepup/send")]
    public async Task<IActionResult> SendStepUpCodeAsync(SendStepUpCodeRequest request, CancellationToken cancellationToken)
    {
        await stepUpService.SendCodeAsync(request.StepUpToken, request.Channel, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("stepup/verify")]
    public async Task<ActionResult<LoginResult>> VerifyStepUpAsync(VerifyStepUpRequest request, CancellationToken cancellationToken)
    {
        var verification = await stepUpService.VerifyLoginAsync(
            request.StepUpToken, request.Channel, request.Credential, cancellationToken);
        var result = await authenticationService.CompleteStepUpLoginAsync(
            verification, request.Mode, request.RememberMe,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("session")]
    public async Task<ActionResult<CurrentSessionInfo>> SessionAsync(CancellationToken cancellationToken)
    {
        var session = await sessionService.GetCurrentAsync(CurrentSid, cancellationToken);
        return session is null ? Unauthorized() : Ok(session);
    }

    [Authorize]
    [HttpPost("session/stepup/send")]
    public async Task<ActionResult<StepUpChallenge>> SendSessionCodeAsync(
        [FromQuery] string channel, CancellationToken cancellationToken)
    {
        var sid = RequireSid();
        var challenge = await stepUpService.SendSessionCodeAsync(sid, channel, cancellationToken);
        return Ok(challenge);
    }

    [Authorize]
    [HttpPost("session/stepup/verify")]
    public async Task<ActionResult<LoginResult>> VerifySessionStepUpAsync(
        SessionStepUpRequest request, CancellationToken cancellationToken)
    {
        var sid = RequireSid();
        await stepUpService.VerifyInSessionAsync(sid, request, cancellationToken);

        // 等级事实已写入会话，需要重签 cookie（刷新 acr claim）并按需重发 token。
        var result = await authenticationService.ReissueAsync(
            sid, "token", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
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
        await authenticationService.LogoutAsync(CurrentSid, cancellationToken);
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

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<AdminUserItem>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var updated = await userService.UpdateProfileAsync(userId, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize]
    [HttpPut("password")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await userService.ChangePasswordAsync(userId, request, cancellationToken);
        return NoContent();
    }

    private string? CurrentSid => User.FindFirstValue(SessionClaims.Sid);

    private string RequireSid() => CurrentSid
        ?? throw new UnauthorizedAccessException("当前凭据没有会话标识，请重新登录。");

    private Guid GetCurrentUserId()
    {
        var id = actorAccessor.Current.ActorId;
        return Guid.TryParse(id, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}

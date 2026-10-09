using System.Security.Claims;
using AdminIdentity.App;
using Auditing.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

/// <summary>
/// 用户自助的会话与设备管理：查看自己在哪些设备上登录、退出其它会话、移除受信设备。
/// 所有操作都先校验归属，避免越权吊销他人凭证。
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize]
[RequestAudit(Feature = "个人中心")]
[Route("api/admin/my")]
public sealed class MySessionsController(
    ISessionService sessionService,
    IDeviceTrustService deviceTrust,
    ICurrentActorAccessor actorAccessor) : ControllerBase
{
    [AuditOperation("查看我的会话")]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionItem>>> ListSessionsAsync(
        [FromQuery] bool includeRevoked, CancellationToken cancellationToken) =>
        Ok(await sessionService.ListAsync(GetCurrentUserId(), includeRevoked, CurrentSid, cancellationToken));

    [AuditOperation("退出会话")]
    [HttpPost("sessions/{sid}/revoke")]
    public async Task<IActionResult> RevokeSessionAsync(string sid, RevokeRequest request, CancellationToken cancellationToken)
    {
        await RequireOwnedSessionAsync(sid, cancellationToken);
        await sessionService.RevokeAsync(
            sid, string.IsNullOrWhiteSpace(request?.Reason) ? "self-revoke" : request.Reason.Trim(), cancellationToken);
        return NoContent();
    }

    [AuditOperation("查看我的设备")]
    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<DeviceItem>>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        return Ok(await deviceTrust.ListDevicesAsync(userId, CurrentDeviceId, CurrentSid, cancellationToken));
    }

    [AuditOperation("移除受信设备")]
    [HttpPost("devices/{deviceId}/revoke")]
    public async Task<IActionResult> RevokeDeviceAsync(string deviceId, RevokeRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await deviceTrust.RevokeDeviceAsync(
            userId, deviceId, string.IsNullOrWhiteSpace(request?.Reason) ? "self-revoke-device" : request.Reason.Trim(), cancellationToken);
        return NoContent();
    }

    private string? CurrentSid => User.FindFirstValue(SessionClaims.Sid);

    private string? CurrentDeviceId => User.FindFirstValue(SessionClaims.DeviceId);

    private async Task RequireOwnedSessionAsync(string sid, CancellationToken cancellationToken)
    {
        var state = await sessionService.GetStateAsync(sid, cancellationToken);

        // 他人的会话统一按"不存在"处理，不泄露 sid 是否有效。
        if (state is null || state.UserId != GetCurrentUserId())
        {
            throw new KeyNotFoundException("Session was not found.");
        }
    }

    private Guid GetCurrentUserId()
    {
        var id = actorAccessor.Current.ActorId;
        return Guid.TryParse(id, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}

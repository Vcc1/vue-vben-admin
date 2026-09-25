using System.Security.Claims;
using AdminIdentity.App;
using Auditing.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

/// <summary>管理员侧的会话与设备治理：查看在线凭证、踢人、吊销设备。</summary>
[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageSessions)]
[RequestAudit]
[Route("api/admin")]
public sealed class AdminSessionsController(
    ISessionService sessionService,
    IDeviceTrustService deviceTrust) : ControllerBase
{
    /// <summary>踢人是不可逆操作，要求会话达到 L2（近期密码核验）。</summary>
    [RequireSessionLevel(SessionLevels.L2)]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionItem>>> ListSessionsAsync(
        [FromQuery] Guid? userId,
        [FromQuery] bool includeRevoked,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.ListAsync(userId, includeRevoked, CurrentSid, cancellationToken));

    [RequireSessionLevel(SessionLevels.L2)]
    [HttpPost("sessions/{sid}/revoke")]
    public async Task<IActionResult> RevokeSessionAsync(string sid, RevokeRequest request, CancellationToken cancellationToken)
    {
        await sessionService.RevokeAsync(sid, Normalize(request?.Reason, "admin-kick"), cancellationToken);
        return NoContent();
    }

    [RequireSessionLevel(SessionLevels.L2)]
    [HttpPost("users/{userId:guid}/sessions/revoke")]
    public async Task<ActionResult<int>> RevokeUserSessionsAsync(Guid userId, RevokeRequest request, CancellationToken cancellationToken)
    {
        var count = await sessionService.RevokeAllForUserAsync(userId, Normalize(request?.Reason, "admin-kick-all"), cancellationToken);
        return Ok(new { revoked = count });
    }

    [RequireSessionLevel(SessionLevels.L2)]
    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<DeviceItem>>> ListDevicesAsync(
        [FromQuery] Guid userId, CancellationToken cancellationToken) =>
        Ok(await deviceTrust.ListDevicesAsync(userId, null, CurrentSid, cancellationToken));

    [RequireSessionLevel(SessionLevels.L2)]
    [HttpPost("users/{userId:guid}/devices/{deviceId}/revoke")]
    public async Task<IActionResult> RevokeDeviceAsync(Guid userId, string deviceId, RevokeRequest request, CancellationToken cancellationToken)
    {
        await deviceTrust.RevokeDeviceAsync(userId, deviceId, Normalize(request?.Reason, "admin-revoke-device"), cancellationToken);
        return NoContent();
    }

    private string? CurrentSid => User.FindFirstValue(SessionClaims.Sid);

    private static string Normalize(string? reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
}

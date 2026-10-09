using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.App;

namespace Notification.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize]
[Route("api/admin/notifications/preferences")]
public sealed class NotificationPreferencesController(
    NotificationPreferenceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserNotificationPreferenceDto>>> GetAsync(CancellationToken ct) =>
        Ok(await service.GetPreferencesAsync(ct));

    [HttpPut]
    public async Task<ActionResult> UpdateAsync(
        [FromBody] UpdatePreferencesRequest request, CancellationToken ct)
    {
        await service.UpdatePreferencesAsync(request.Preferences, ct);
        return Ok();
    }
}

public sealed record UpdatePreferencesRequest(IReadOnlyList<UserNotificationPreferenceInput> Preferences);

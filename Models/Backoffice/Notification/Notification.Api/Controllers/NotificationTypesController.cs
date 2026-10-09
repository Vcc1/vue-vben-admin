using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.App;

namespace Notification.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AdminPolicies.View)]
[Route("api/admin/notification-types")]
public sealed class NotificationTypesController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationTypeDto>>> ListAsync(CancellationToken ct) =>
        Ok(await service.ListTypesAsync(ct));

    [Authorize(Policy = AdminPolicies.ManageNotifications)]
    [HttpPost]
    public async Task<ActionResult<NotificationTypeDto>> CreateAsync(
        NotificationTypeInput input, CancellationToken ct)
    {
        var type = await service.CreateTypeAsync(input, ct);
        return Created($"/api/admin/notification-types/{type.Id}", type);
    }

    [Authorize(Policy = AdminPolicies.ManageNotifications)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationTypeDto>> UpdateAsync(
        Guid id, NotificationTypeInput input, CancellationToken ct) =>
        Ok(await service.UpdateTypeAsync(id, input, "", ct));

    [Authorize(Policy = AdminPolicies.ManageNotifications)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await service.DeleteTypeAsync(id, ct);
        return Ok();
    }
}

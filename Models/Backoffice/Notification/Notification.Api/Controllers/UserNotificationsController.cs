using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notification.Contracts;

namespace Notification.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize]
[Route("api/admin/notifications")]
public sealed class UserNotificationsController(
    App.UserNotificationService service,
    INotificationPublisher publisher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<App.UserNotificationPage>> ListAsync(
        [FromQuery] string? typeCode,
        [FromQuery] bool? isRead,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var query = new App.NotificationQuery(typeCode, isRead, skip, take);
        return Ok(await service.GetNotificationsAsync(query, ct));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> UnreadCountAsync(CancellationToken ct) =>
        Ok(await service.GetUnreadCountAsync(ct));

    [HttpPost("mark-read")]
    public async Task<ActionResult> MarkReadAsync(
        [FromBody] MarkReadRequest request, CancellationToken ct)
    {
        await service.MarkAsReadAsync(request.Ids, ct);
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public async Task<ActionResult> MarkAllReadAsync(CancellationToken ct)
    {
        await service.MarkAllAsReadAsync(ct);
        return Ok();
    }

    [Authorize(Policy = AdminPolicies.ManageNotifications)]
    [HttpPost("publish")]
    public async Task<ActionResult> PublishAsync(
        [FromBody] PublishRequest request, CancellationToken ct)
    {
        var draft = new NotificationDraft(
            request.TypeCode,
            request.Title,
            request.Summary,
            request.ContentJson,
            request.LinkUrl,
            new NotificationTarget(request.Scope, request.TargetIds),
            SourceEventKey: null);

        await publisher.PublishAsync(draft, NotificationSources.Manual, ct);
        return Ok();
    }
}

public sealed record MarkReadRequest(IReadOnlyList<Guid> Ids);

public sealed record PublishRequest(
    string TypeCode,
    string Title,
    string Summary,
    string ContentJson,
    string? LinkUrl,
    string Scope,
    IReadOnlyList<string> TargetIds);

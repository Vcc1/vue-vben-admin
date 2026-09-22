using AdminIdentity.App;
using Auditing.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditing.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.View)]
[Route("api/admin/auditing")]
public sealed class AuditQueryController(IAuditQueryService service) : ControllerBase
{
    [HttpGet("request-logs")]
    public async Task<ActionResult<IReadOnlyList<RequestAuditLogItem>>> QueryRequestLogsAsync(
        [FromQuery] string? actorName,
        [FromQuery] string? path,
        [FromQuery] string? httpMethod,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(await service.QueryRequestLogsAsync(
            new AuditQueryFilter(actorName, path, httpMethod, null, null, fromUtc, toUtc, take), ct));

    [HttpGet("security-events")]
    public async Task<ActionResult<IReadOnlyList<SecurityEventItem>>> QuerySecurityEventsAsync(
        [FromQuery] string? actorName,
        [FromQuery] string? eventType,
        [FromQuery] string? severity,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(await service.QuerySecurityEventsAsync(
            new AuditQueryFilter(actorName, null, null, eventType, severity, fromUtc, toUtc, take), ct));
}

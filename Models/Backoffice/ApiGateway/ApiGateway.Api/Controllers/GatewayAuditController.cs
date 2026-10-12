using ApiGateway.App;
using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.View)]
[Route("api/admin/gateway/audit-logs")]
public sealed class GatewayAuditController(IGatewayAuditService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GatewayAuditLogItem>>> QueryAsync(
        [FromQuery] Guid? apiId,
        [FromQuery] Guid? moduleId,
        [FromQuery] string? actorName,
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        Ok(await service.QueryAsync(apiId, moduleId, actorName, take, ct));
}

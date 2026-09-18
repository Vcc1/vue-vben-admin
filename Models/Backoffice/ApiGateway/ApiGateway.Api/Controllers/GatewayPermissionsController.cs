using ApiGateway.App;
using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/gateway/permissions")]
public sealed class GatewayPermissionsController(IGatewayPermissionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GatewayPermissionItem>>> ListAsync(
        [FromQuery] Guid? moduleId, CancellationToken ct) =>
        Ok(await service.ListPermissionsAsync(moduleId, ct));

    [HttpPost]
    public async Task<ActionResult<GatewayPermissionItem>> GrantAsync(
        CreateGatewayPermissionRequest request, CancellationToken ct)
    {
        var result = await service.GrantAsync(request, ct);
        return Created($"/api/admin/gateway/permissions/{result.Id}", result);
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<GatewayPermissionItem>>> BatchGrantAsync(
        BatchGrantPermissionRequest request, CancellationToken ct) =>
        Ok(await service.BatchGrantAsync(request, ct));

    [HttpDelete("{permissionId:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid permissionId, CancellationToken ct)
    {
        await service.RevokeAsync(permissionId, ct);
        return NoContent();
    }

    [HttpPut("{permissionId:guid}/enabled")]
    public async Task<IActionResult> SetEnabledAsync(
        Guid permissionId, [FromBody] SetPermissionEnabledRequest request, CancellationToken ct)
    {
        await service.SetEnabledAsync(permissionId, request.IsEnabled, ct);
        return NoContent();
    }
}

public sealed record SetPermissionEnabledRequest(bool IsEnabled);

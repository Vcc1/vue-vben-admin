using ApiGateway.App;
using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/gateway")]
public sealed class GatewayModulesController(
    IGatewayModuleService moduleService,
    IGatewayEndpointService endpointService) : ControllerBase
{
    // ──────────────── Module CRUD ────────────────

    [HttpGet("apis/{apiId:guid}/modules")]
    public async Task<ActionResult<IReadOnlyList<GatewayModuleItem>>> ListModulesAsync(
        Guid apiId, CancellationToken ct) =>
        Ok(await moduleService.ListModulesAsync(apiId, ct));

    [HttpPost("apis/{apiId:guid}/modules")]
    public async Task<ActionResult<GatewayModuleItem>> CreateModuleAsync(
        Guid apiId, CreateGatewayModuleRequest request, CancellationToken ct)
    {
        var result = await moduleService.CreateModuleAsync(apiId, request, ct);
        return Created($"/api/admin/gateway/modules/{result.Id}", result);
    }

    [HttpPut("modules/{moduleId:guid}")]
    public async Task<ActionResult<GatewayModuleItem>> UpdateModuleAsync(
        Guid moduleId, UpdateGatewayModuleRequest request, CancellationToken ct) =>
        Ok(await moduleService.UpdateModuleAsync(moduleId, request, ct));

    [HttpDelete("modules/{moduleId:guid}")]
    public async Task<IActionResult> DeleteModuleAsync(Guid moduleId, CancellationToken ct)
    {
        await moduleService.DeleteModuleAsync(moduleId, ct);
        return NoContent();
    }

    // ──────────────── Endpoint CRUD ────────────────

    [HttpGet("modules/{moduleId:guid}/endpoints")]
    public async Task<ActionResult<IReadOnlyList<GatewayEndpointItem>>> ListEndpointsAsync(
        Guid moduleId, CancellationToken ct) =>
        Ok(await endpointService.ListEndpointsAsync(moduleId, ct));

    [HttpPost("modules/{moduleId:guid}/endpoints")]
    public async Task<ActionResult<GatewayEndpointItem>> CreateEndpointAsync(
        Guid moduleId, CreateGatewayEndpointRequest request, CancellationToken ct)
    {
        var result = await endpointService.CreateEndpointAsync(moduleId, request, ct);
        return Created($"/api/admin/gateway/endpoints/{result.Id}", result);
    }

    [HttpPost("modules/{moduleId:guid}/endpoints/batch")]
    public async Task<ActionResult<IReadOnlyList<GatewayEndpointItem>>> BatchCreateEndpointsAsync(
        Guid moduleId, [FromBody] IReadOnlyList<CreateGatewayEndpointRequest> requests, CancellationToken ct) =>
        Ok(await endpointService.BatchCreateEndpointsAsync(moduleId, requests, ct));

    [HttpDelete("endpoints/{endpointId:guid}")]
    public async Task<IActionResult> DeleteEndpointAsync(Guid endpointId, CancellationToken ct)
    {
        await endpointService.DeleteEndpointAsync(endpointId, ct);
        return NoContent();
    }
}

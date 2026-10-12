using ApiGateway.App;
using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/gateway/apis")]
public sealed class GatewayApisController(IGatewayApiService apiService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GatewayApiItem>>> ListAsync(CancellationToken ct) =>
        Ok(await apiService.ListApisAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GatewayApiDetail>> GetDetailAsync(Guid id, CancellationToken ct) =>
        Ok(await apiService.GetApiDetailAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<GatewayApiItem>> CreateAsync(
        CreateGatewayApiRequest request, CancellationToken ct)
    {
        var result = await apiService.CreateApiAsync(request, ct);
        return Created($"/api/admin/gateway/apis/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GatewayApiItem>> UpdateAsync(
        Guid id, UpdateGatewayApiRequest request, CancellationToken ct) =>
        Ok(await apiService.UpdateApiAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        await apiService.DeleteApiAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/enabled")]
    public async Task<IActionResult> SetEnabledAsync(
        Guid id, [FromBody] SetEnabledRequest request, CancellationToken ct)
    {
        await apiService.SetApiEnabledAsync(id, request.IsEnabled, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/import/diff")]
    public async Task<ActionResult<ImportDiffResult>> GetImportDiffAsync(
        Guid id, ImportDiffRequest request, CancellationToken ct) =>
        Ok(await apiService.GetImportDiffAsync(id, request, ct));

    [HttpPost("{id:guid}/import/apply")]
    public async Task<IActionResult> ApplyImportAsync(
        Guid id, ApplyImportRequest request, CancellationToken ct)
    {
        await apiService.ApplyImportAsync(id, request, ct);
        return NoContent();
    }
}

public sealed record SetEnabledRequest(bool IsEnabled);

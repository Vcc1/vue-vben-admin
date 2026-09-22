using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/permissions")]
public sealed class AdminPermissionsController(IAdminAccessManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPermissionNode>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListPermissionsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AdminPermissionNode>> CreateAsync(CreateAdminPermissionRequest request, CancellationToken cancellationToken)
    {
        var item = await service.CreatePermissionAsync(request, cancellationToken);
        return Created($"/api/admin/permissions/{Uri.EscapeDataString(item.Code)}", item);
    }

    [HttpPut("{code}")]
    public async Task<ActionResult<AdminPermissionNode>> UpdateAsync(string code, UpdateAdminPermissionRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdatePermissionAsync(code, request, cancellationToken));

    [HttpDelete("{code}")]
    public async Task<IActionResult> DeleteAsync(string code, CancellationToken cancellationToken)
    {
        await service.DeletePermissionAsync(code, cancellationToken);
        return NoContent();
    }
}

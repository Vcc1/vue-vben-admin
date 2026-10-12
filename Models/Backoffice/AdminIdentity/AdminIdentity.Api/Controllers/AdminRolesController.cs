using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/roles")]
public sealed class AdminRolesController(IAdminAccessManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminRoleItem>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListRolesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AdminRoleItem>> CreateAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await service.CreateRoleAsync(request, cancellationToken);
        return Created($"/api/admin/roles/{Uri.EscapeDataString(role.Name)}", role);
    }

    [HttpPut("{roleName}/permissions")]
    public async Task<IActionResult> SetPermissionsAsync(string roleName, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        await service.SetRolePermissionsAsync(roleName, request.Permissions, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{roleName}")]
    public async Task<IActionResult> DeleteAsync(string roleName, CancellationToken cancellationToken)
    {
        await service.DeleteRoleAsync(roleName, cancellationToken);
        return NoContent();
    }
}

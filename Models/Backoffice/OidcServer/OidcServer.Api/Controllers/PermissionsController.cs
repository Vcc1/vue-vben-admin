using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

namespace OidcServer.Api.Controllers;

[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public sealed class PermissionsController(IRolePermissionCatalog rolePermissionCatalog, AdminDbContext db) : Controller
{
    [HttpGet("~/connect/permissions/roles")]
    [Produces("application/json")]
    public IActionResult GetRolePermissions()
    {
        return Ok(rolePermissionCatalog.GetRolePermissions());
    }

    [HttpGet("~/connect/permissions/catalog")]
    [Produces("application/json")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var entries = await db.Permissions.AsNoTracking()
            .Select(p => new AdminPermissionEntry(p.Code, p.Name, p.Description, p.IsEnabled))
            .ToListAsync(cancellationToken);
        return Ok(AdminPermissionTree.Build(entries));
    }
}

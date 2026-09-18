using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OidcServer.App;

namespace OidcServer.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/oidc/scopes")]
public sealed class OidcScopeController(IOidcScopeService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OidcScopeItem>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OidcScopeItem>> CreateAsync(CreateOidcScopeRequest request, CancellationToken cancellationToken)
    {
        var scope = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/oidc/scopes/{scope.Id}", scope);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

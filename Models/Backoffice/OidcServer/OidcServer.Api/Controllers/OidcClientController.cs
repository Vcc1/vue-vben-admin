using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OidcServer.App;

namespace OidcServer.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/oidc/clients")]
public sealed class OidcClientController(IOidcClientService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OidcClientItem>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OidcClientItem>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await service.GetAsync(id, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    public async Task<ActionResult<OidcClientItem>> CreateAsync(CreateOidcClientRequest request, CancellationToken cancellationToken)
    {
        var client = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/oidc/clients/{client.Id}", client);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OidcClientItem>> UpdateAsync(
        Guid id, UpdateOidcClientRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/regenerate-secret")]
    public async Task<ActionResult<object>> RegenerateSecretAsync(Guid id, CancellationToken cancellationToken)
    {
        var secret = await service.RegenerateSecretAsync(id, cancellationToken);
        return Ok(new { secret });
    }
}

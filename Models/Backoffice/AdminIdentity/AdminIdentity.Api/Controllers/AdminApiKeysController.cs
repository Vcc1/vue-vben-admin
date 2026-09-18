using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/api-keys")]
public sealed class AdminApiKeysController(IAdminApiKeyService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApiKeyItem>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CreatedApiKey>> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/api-keys/{result.Id}", result);
    }

    [HttpDelete("{apiKeyId:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken)
    {
        await service.RevokeAsync(apiKeyId, cancellationToken);
        return NoContent();
    }
}

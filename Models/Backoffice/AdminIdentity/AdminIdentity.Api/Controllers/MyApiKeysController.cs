using System.Security.Claims;
using AdminIdentity.App;
using Auditing.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

/// <summary>
/// 用户自助的 API Key 管理：创建、查看、撤销自己的 API Key。
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize]
[RequestAudit(Feature = "个人中心")]
[Route("api/admin/my")]
public sealed class MyApiKeysController(
    IAdminApiKeyService apiKeyService,
    ICurrentActorAccessor actorAccessor) : ControllerBase
{
    [AuditOperation("查看我的 API Key")]
    [HttpGet("api-keys")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyItem>>> ListAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var allKeys = await apiKeyService.ListAsync(cancellationToken);
        return Ok(allKeys.Where(k => k.UserId == userId).ToList());
    }

    [AuditOperation("创建我的 API Key")]
    [HttpPost("api-keys")]
    public async Task<ActionResult<CreatedApiKey>> CreateAsync(CreateMyApiKeyRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await apiKeyService.CreateAsync(new CreateApiKeyRequest(userId, request.Name, request.ExpiresAtUtc), cancellationToken);
        return Created($"/api/admin/api-keys/{result.Id}", result);
    }

    [AuditOperation("撤销我的 API Key")]
    [HttpDelete("api-keys/{apiKeyId:guid}")]
    public async Task<IActionResult> RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var allKeys = await apiKeyService.ListAsync(cancellationToken);
        var key = allKeys.FirstOrDefault(k => k.Id == apiKeyId);
        if (key is null || key.UserId != userId)
        {
            throw new KeyNotFoundException("API Key was not found.");
        }
        await apiKeyService.RevokeAsync(apiKeyId, cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var id = actorAccessor.Current.ActorId;
        return Guid.TryParse(id, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}

public sealed record CreateMyApiKeyRequest(string Name, DateTime? ExpiresAtUtc);

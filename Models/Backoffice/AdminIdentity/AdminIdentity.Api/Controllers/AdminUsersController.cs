using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[Route("api/admin/users")]
public sealed class AdminUsersController(IAdminUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserItem>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AdminUserItem>> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var user = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/users/{user.Id}", user);
    }

    [HttpPut("{userId:guid}/enabled")]
    public async Task<IActionResult> SetEnabledAsync(Guid userId, SetUserEnabledRequest request, CancellationToken cancellationToken)
    {
        await service.SetEnabledAsync(userId, request.IsEnabled, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/password")]
    public async Task<IActionResult> ResetPasswordAsync(Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.ResetPasswordAsync(userId, request.NewPassword, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        await service.SetRolesAsync(userId, request.Roles, cancellationToken);
        return NoContent();
    }
}

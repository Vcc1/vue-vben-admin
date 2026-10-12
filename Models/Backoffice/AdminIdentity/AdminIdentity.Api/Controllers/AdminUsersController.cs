using AdminIdentity.App;
using Auditing.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminIdentity.Api.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = OpenApiMetadata.DocumentName)]
[Authorize(Policy = AdminPolicies.ManageUsers)]
[RequestAudit(Feature = "用户管理", SensitivePaths = ["password", "newPassword"])]
[Route("api/admin/users")]
public sealed class AdminUsersController(IAdminUserService service) : ControllerBase
{
    [HttpGet]
    [AuditOperation("查询用户列表")]
    public async Task<ActionResult<IReadOnlyList<AdminUserItem>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpPost]
    [AuditOperation("新建用户")]
    public async Task<ActionResult<AdminUserItem>> CreateAsync(CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var user = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/users/{user.Id}", user);
    }

    [HttpPut("{userId:guid}/enabled")]
    [AuditOperation("启用/停用用户")]
    public async Task<IActionResult> SetEnabledAsync(Guid userId, SetUserEnabledRequest request, CancellationToken cancellationToken)
    {
        await service.SetEnabledAsync(userId, request.IsEnabled, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/password")]
    [AuditOperation("重置密码")]
    public async Task<IActionResult> ResetPasswordAsync(Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.ResetPasswordAsync(userId, request.NewPassword, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/roles")]
    [AuditOperation("分配角色")]
    public async Task<IActionResult> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        await service.SetRolesAsync(userId, request.Roles, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/profile")]
    [AuditOperation("修改用户资料")]
    public async Task<ActionResult<AdminUserItem>> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await service.UpdateUserAsync(userId, request, cancellationToken);
        return Ok(user);
    }
}

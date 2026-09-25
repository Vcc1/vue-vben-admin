using System.Security.Claims;
using AdminIdentity.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcServer.Api.Controllers;

[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public sealed class UserInfoController(
    IRolePermissionCatalog rolePermissionCatalog,
    ISessionService sessions) : Controller
{
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> HandleUserInfo(CancellationToken cancellationToken)
    {
        var claims = User.Claims
            .Where(c => c.Type is Claims.Subject
                     or Claims.Name
                     or Claims.PreferredUsername
                     or Claims.Email
                     or Claims.Role
                     or SessionClaims.Amr
                     or SessionClaims.AuthTime
                     or "display_name")
            .GroupBy(c => c.Type)
            .ToDictionary(
                g => g.Key,
                g => (object)(g.Count() == 1 ? g.First().Value : g.Select(c => c.Value).ToArray()));

        // token 里的 acr 只是签发时刻的快照，会随掉窗而过期；userinfo 返回实时等级，
        // 第三方需要判断"当前还能不能做敏感操作"时必须走这里。
        var sid = User.FindFirstValue(SessionClaims.Sid);
        claims[SessionClaims.Acr] = SessionLevels.ToAcr(await sessions.GetLevelAsync(sid, cancellationToken));

        // 权限码较长且会变更，不放进 JWT；由 UserInfo 端点按角色实时解析下发给前端
        var mapping = rolePermissionCatalog.GetRolePermissions();
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in User.FindAll(Claims.Role).Select(c => c.Value))
        {
            if (mapping.TryGetValue(role, out var codes))
            {
                foreach (var code in codes)
                {
                    permissions.Add(code);
                }
            }
        }

        claims["permission"] = permissions.ToArray();

        return Ok(claims);
    }
}

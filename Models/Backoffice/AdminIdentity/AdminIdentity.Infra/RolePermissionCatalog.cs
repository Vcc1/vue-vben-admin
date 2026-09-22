using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdminIdentity.Infra;

/// <summary>
/// 角色→权限映射的内存快照。数据来自数据库表 Identity_Permissions / Identity_RolePermissions，
/// 由 <see cref="RolePermissionCacheRefresher"/> 定时刷新；读取永远是内存操作，不落库、不打数据库。
/// </summary>
public sealed class RolePermissionCatalog(
    IServiceScopeFactory scopeFactory,
    ILogger<RolePermissionCatalog> logger) : IRolePermissionCatalog
{
    private volatile IReadOnlyDictionary<string, IReadOnlyList<string>> _snapshot =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetRolePermissions() => _snapshot;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

            var allCodes = await db.Permissions
                .Where(p => p.IsEnabled)
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);
            var enabledCodes = allCodes.ToHashSet(StringComparer.Ordinal);

            var rows = await db.RolePermissions
                .Select(rp => new { rp.RoleName, rp.PermissionCode })
                .ToListAsync(cancellationToken);

            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in rows.GroupBy(x => x.RoleName, StringComparer.OrdinalIgnoreCase))
            {
                var codes = new List<string>();
                foreach (var code in group.Select(x => x.PermissionCode))
                {
                    if (!AdminPermissions.IsGrantCode(code))
                    {
                        continue;
                    }
                    if ((code.Contains('*') || enabledCodes.Contains(code)) &&
                        !codes.Contains(code, StringComparer.Ordinal))
                    {
                        codes.Add(code);
                    }
                }

                result[group.Key] = codes;
            }

            _snapshot = result;
            logger.LogInformation("已刷新角色权限缓存：{RoleCount} 个角色，{PermissionCount} 个启用权限码。",
                result.Count, allCodes.Count);
        }
        catch (Exception ex)
        {
            // 刷新失败保留上一次快照，避免数据库抖动导致全站 403
            logger.LogError(ex, "刷新角色权限缓存失败，继续沿用上一次快照。");
        }
    }
}

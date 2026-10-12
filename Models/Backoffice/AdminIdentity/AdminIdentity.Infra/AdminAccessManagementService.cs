using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdminIdentity.Infra;

internal sealed class AdminAccessManagementService(
    AdminDbContext db,
    RoleManager<AdminRole> roleManager,
    IRolePermissionCatalog catalog,
    ICurrentActorAccessor actorAccessor,
    TimeProvider timeProvider) : IAdminAccessManagementService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<IReadOnlyList<AdminRoleItem>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var counts = await db.UserRoles.GroupBy(x => x.RoleId)
            .Select(x => new { RoleId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);
        var grants = await db.RolePermissions.AsNoTracking().ToListAsync(cancellationToken);
        return roles.Select(role => new AdminRoleItem(
            role.Name!, AdminRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase),
            counts.GetValueOrDefault(role.Id),
            grants.Where(x => string.Equals(x.RoleName, role.Name, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.PermissionCode).Order(StringComparer.Ordinal).ToArray())).ToArray();
    }

    public async Task<AdminRoleItem> CreateRoleAsync(CreateAdminRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ArgumentException("角色名称必须为 1 至 100 个字符。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await roleManager.CreateAsync(new AdminRole { Id = Guid.CreateVersion7(), Name = name });
        EnsureSucceeded(result);
        await AuditAsync("CreateRole", new { Name = name }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdminRoleItem(name, false, 0, []);
    }

    public async Task SetRolePermissionsAsync(string roleName, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var codes = permissions.Select(x => x?.Trim() ?? "").Distinct(StringComparer.Ordinal).ToArray();
        if (codes.Any(x => x.Length > 100 || !AdminPermissions.IsGrantCode(x)))
            throw new ArgumentException("授权码不能超过 100 个字符，且仅支持具体权限、* 或以 :* 结尾的层级通配符。");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var role = await RequiredRoleAsync(roleName);
        var exact = codes.Where(x => !x.Contains('*')).ToArray();
        var known = await db.Permissions.Where(x => exact.Contains(x.Code))
            .Select(x => x.Code).ToListAsync(cancellationToken);
        if (exact.Except(known, StringComparer.Ordinal).Any())
            throw new ArgumentException("具体授权码必须存在于权限目录中。");

        var previous = await db.RolePermissions.Where(x => x.RoleName == role.Name)
            .ToListAsync(cancellationToken);
        var oldCodes = previous.Select(x => x.PermissionCode).ToHashSet(StringComparer.Ordinal);
        var newCodes = codes.ToHashSet(StringComparer.Ordinal);
        db.RolePermissions.RemoveRange(previous.Where(x => !newCodes.Contains(x.PermissionCode)));
        db.RolePermissions.AddRange(codes.Where(code => !oldCodes.Contains(code)).Select(code => new AdminRolePermission
        {
            RoleName = role.Name!, PermissionCode = code
        }));
        await AuditAsync("SetRolePermissions", new { Role = role.Name, Before = oldCodes, After = codes }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await catalog.RefreshAsync(cancellationToken);
    }

    public async Task DeleteRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var role = await RequiredRoleAsync(roleName);
        if (AdminRoles.All.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("内置角色不能删除。");
        if (await db.UserRoles.AnyAsync(x => x.RoleId == role.Id, cancellationToken))
            throw new InvalidOperationException("角色仍分配给用户，不能删除。");

        var grants = await db.RolePermissions.Where(x => x.RoleName == role.Name).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(grants);
        await db.SaveChangesAsync(cancellationToken);
        EnsureSucceeded(await roleManager.DeleteAsync(role));
        await AuditAsync("DeleteRole", new { Role = role.Name, Permissions = grants.Select(x => x.PermissionCode) }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await catalog.RefreshAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPermissionNode>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Permissions.AsNoTracking()
            .Select(x => new AdminPermissionEntry(x.Code, x.Name, x.Description, x.IsEnabled))
            .ToListAsync(cancellationToken);
        return AdminPermissionTree.Build(rows);
    }

    public async Task<AdminPermissionNode> CreatePermissionAsync(CreateAdminPermissionRequest request, CancellationToken cancellationToken)
    {
        if (request.Code is null || request.Code.Length > 100 || !AdminPermissions.IsCatalogCode(request.Code))
            throw new ArgumentException("权限码不能为空、包含空层级或星号，且不能超过 100 个字符。");
        ValidatePermissionDetails(request.Name, request.Description);
        if (await db.Permissions.AnyAsync(x => x.Code == request.Code, cancellationToken))
            throw new InvalidOperationException("权限码已存在。");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new AdminPermission
        {
            Code = request.Code, Name = request.Name.Trim(), Description = request.Description?.Trim(),
            IsEnabled = request.IsEnabled, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Permissions.Add(entity);
        await AuditAsync("CreatePermission", new { entity.Code, entity.Name, entity.Description, entity.IsEnabled }, cancellationToken);
        await catalog.RefreshAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<AdminPermissionNode> UpdatePermissionAsync(string code, UpdateAdminPermissionRequest request, CancellationToken cancellationToken)
    {
        ValidatePermissionDetails(request.Name, request.Description);
        var entity = await db.Permissions.FindAsync([code], cancellationToken)
            ?? throw new KeyNotFoundException("权限不存在。");
        var before = new { entity.Name, entity.Description, entity.IsEnabled };
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await AuditAsync("UpdatePermission", new { entity.Code, Before = before, entity.Name, entity.Description, entity.IsEnabled }, cancellationToken);
        await catalog.RefreshAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeletePermissionAsync(string code, CancellationToken cancellationToken)
    {
        var entity = await db.Permissions.FindAsync([code], cancellationToken)
            ?? throw new KeyNotFoundException("权限不存在。");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var grants = await db.RolePermissions.Where(x => x.PermissionCode == code).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(grants);
        db.Permissions.Remove(entity);
        await AuditAsync("DeletePermission", new { entity.Code, entity.Name, Roles = grants.Select(x => x.RoleName) }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await catalog.RefreshAsync(cancellationToken);
    }

    private async Task<AdminRole> RequiredRoleAsync(string name) =>
        await roleManager.FindByNameAsync(name)
        ?? throw new KeyNotFoundException("角色不存在。");

    private static void ValidatePermissionDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("权限名称必须为 1 至 200 个字符。");
        if (description?.Trim().Length > 500)
            throw new ArgumentException("权限说明不能超过 500 个字符。");
    }

    private static AdminPermissionNode Map(AdminPermission entity) => new()
    {
        Code = entity.Code, Name = entity.Name, Description = entity.Description, IsEnabled = entity.IsEnabled
    };

    private async Task AuditAsync(string action, object details, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current;
        db.UserAuditLogs.Add(new AdminUserAuditLog
        {
            Action = action, ActorType = actor.ActorType, ActorId = actor.ActorId,
            ActorName = actor.ActorName, AuthenticationType = actor.AuthenticationType,
            AfterJson = JsonSerializer.Serialize(details, AuditJsonOptions),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            IpAddress = actor.IpAddress, UserAgent = actor.UserAgent,
            CorrelationId = actor.CorrelationId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}

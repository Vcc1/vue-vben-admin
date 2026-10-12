using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdminIdentity.Infra;

internal sealed class AdminAccessTransferService(
    AdminDbContext db,
    RoleManager<AdminRole> roleManager,
    IRolePermissionCatalog catalog,
    ICurrentActorAccessor actorAccessor,
    TimeProvider timeProvider) : IAdminAccessTransferService
{
    private const string Format = "mengtu-admin-access";
    private const int Version = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task<AccessTransferDocument> ExportAsync(string kind, CancellationToken cancellationToken) =>
        (await LoadAsync(ValidateKind(kind), cancellationToken)).Document;

    public async Task<AccessTransferPreview> CompareAsync(
        AccessTransferDocument document, CancellationToken cancellationToken)
    {
        ValidateDocument(document);
        var current = await LoadAsync(document.Kind, cancellationToken);
        return BuildPreview(document, current);
    }

    public async Task<AccessTransferApplyResult> ApplyAsync(
        AccessTransferApplyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDocument(request.Document);
        if (request.SelectedKeys is null || request.SelectedKeys.Count == 0)
            throw new ArgumentException("请先勾选需要应用的差异。");
        if (request.SelectedKeys.Count != request.SelectedKeys.Distinct(StringComparer.Ordinal).Count())
            throw new ArgumentException("勾选项不能重复。");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var current = await LoadAsync(request.Document.Kind, cancellationToken);
        var preview = BuildPreview(request.Document, current);
        if (!string.Equals(preview.Baseline, request.Baseline, StringComparison.Ordinal))
            throw new InvalidOperationException("目标环境数据已变化，请重新上传文件并比对。");

        var byKey = preview.Changes.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var selected = request.SelectedKeys.Select(key =>
            byKey.TryGetValue(key, out var change) ? change :
                throw new ArgumentException($"差异项不存在：{key}。"))
            .ToArray();
        var blocked = selected.FirstOrDefault(x => x.BlockReason is not null);
        if (blocked is not null)
            throw new InvalidOperationException($"{blocked.Key}：{blocked.BlockReason}");

        if (request.Document.Kind == "permissions")
            await ApplyPermissionsAsync(selected, cancellationToken);
        else
            await ApplyRolesAsync(selected, cancellationToken);

        var actor = actorAccessor.Current;
        db.UserAuditLogs.Add(new AdminUserAuditLog
        {
            Action = request.Document.Kind == "permissions" ? "ImportPermissions" : "ImportRoles",
            ActorType = actor.ActorType, ActorId = actor.ActorId, ActorName = actor.ActorName,
            AuthenticationType = actor.AuthenticationType, IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent, CorrelationId = actor.CorrelationId,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            AfterJson = JsonSerializer.Serialize(selected, JsonOptions)
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await catalog.RefreshAsync(cancellationToken);
        return new AccessTransferApplyResult(selected.Length);
    }

    private async Task ApplyPermissionsAsync(AccessTransferChange[] changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            var existing = await db.Permissions.FindAsync([change.Key], cancellationToken);
            if (change.Action == "delete")
            {
                var grants = await db.RolePermissions.Where(x => x.PermissionCode == change.Key)
                    .ToListAsync(cancellationToken);
                db.RolePermissions.RemoveRange(grants);
                db.Permissions.Remove(existing!);
                continue;
            }

            var incoming = change.AfterPermission!;
            var row = existing ?? new AdminPermission
            {
                Code = incoming.Code,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            row.Name = incoming.Name;
            row.Description = incoming.Description;
            row.IsEnabled = incoming.IsEnabled;
            row.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            if (existing is null) db.Permissions.Add(row);
        }
    }

    private async Task ApplyRolesAsync(AccessTransferChange[] changes, CancellationToken cancellationToken)
    {
        var known = await db.Permissions.Select(x => x.Code).ToListAsync(cancellationToken);
        foreach (var change in changes)
        {
            var role = await roleManager.FindByNameAsync(change.Key);
            if (change.Action == "delete")
            {
                if (AdminRoles.All.Contains(role!.Name, StringComparer.OrdinalIgnoreCase) ||
                    await db.UserRoles.AnyAsync(x => x.RoleId == role.Id, cancellationToken))
                    throw new InvalidOperationException($"角色 {change.Key} 仍受保护或已分配给用户，请重新比对。");
                var grants = await db.RolePermissions.Where(x => x.RoleName == role.Name)
                    .ToListAsync(cancellationToken);
                db.RolePermissions.RemoveRange(grants);
                await db.SaveChangesAsync(cancellationToken);
                EnsureSucceeded(await roleManager.DeleteAsync(role));
                continue;
            }

            var incoming = change.AfterRole!;
            var missing = incoming.Permissions.Where(x => !x.Contains('*'))
                .Except(known, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"角色 {incoming.Name} 引用了不存在的权限：{string.Join(", ", missing)}。");
            if (role is null)
            {
                role = new AdminRole { Id = Guid.CreateVersion7(), Name = incoming.Name };
                EnsureSucceeded(await roleManager.CreateAsync(role));
            }

            var old = await db.RolePermissions.Where(x => x.RoleName == role.Name)
                .ToListAsync(cancellationToken);
            var desired = incoming.Permissions.ToHashSet(StringComparer.Ordinal);
            var existing = old.Select(x => x.PermissionCode).ToHashSet(StringComparer.Ordinal);
            db.RolePermissions.RemoveRange(old.Where(x => !desired.Contains(x.PermissionCode)));
            db.RolePermissions.AddRange(incoming.Permissions.Where(x => !existing.Contains(x))
                .Select(x => new AdminRolePermission { RoleName = role.Name!, PermissionCode = x }));
        }
    }

    private static AccessTransferPreview BuildPreview(AccessTransferDocument incoming, Snapshot current)
    {
        var changes = new List<AccessTransferChange>();
        if (incoming.Kind == "permissions")
        {
            var source = incoming.Permissions!.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
            var target = current.Document.Permissions!.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in source.Values.OrderBy(x => x.Code, StringComparer.Ordinal))
            {
                target.TryGetValue(entry.Code, out var before);
                var casingConflict = before is not null && !string.Equals(before.Code, entry.Code, StringComparison.Ordinal);
                if (!casingConflict && before is not null && before.Name == entry.Name &&
                    before.Description == entry.Description && before.IsEnabled == entry.IsEnabled) continue;
                changes.Add(new(before?.Code ?? entry.Code, before is null ? "add" : "update",
                    before, entry, null, null,
                    casingConflict ? "权限码仅大小写不同；授权按大小写匹配，请先手动统一权限码。" : null, null));
            }
            foreach (var entry in target.Values.OrderBy(x => x.Code, StringComparer.Ordinal))
            {
                if (source.ContainsKey(entry.Code)) continue;
                var grantRoles = current.ExactGrantRoles.GetValueOrDefault(entry.Code) ?? [];
                changes.Add(new(entry.Code, "delete", entry, null, null, null, null,
                    grantRoles.Length > 0
                        ? $"将同时移除 {grantRoles.Length} 条精确角色授权：{string.Join("、", grantRoles)}"
                        : null));
            }
        }
        else
        {
            var source = incoming.Roles!.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var target = current.Document.Roles!.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var knownCodes = current.KnownCodes;
            foreach (var entry in source.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                target.TryGetValue(entry.Name, out var before);
                var casingConflict = before is not null && !string.Equals(before.Name, entry.Name, StringComparison.Ordinal);
                if (!casingConflict && before is not null && before.Permissions.Order(StringComparer.Ordinal)
                    .SequenceEqual(entry.Permissions.Order(StringComparer.Ordinal))) continue;
                var missing = entry.Permissions.Where(x => !x.Contains('*') && !knownCodes.Contains(x)).ToArray();
                changes.Add(new(before?.Name ?? entry.Name, before is null ? "add" : "update",
                    null, null, before, entry,
                    casingConflict ? "角色名仅大小写不同；请先手动统一角色名。" :
                    missing.Length == 0 ? null : $"目标权限目录缺少：{string.Join(", ", missing)}。请先导入权限。", null));
            }
            foreach (var entry in target.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (source.ContainsKey(entry.Name)) continue;
                var count = current.UserCounts.GetValueOrDefault(entry.Name);
                var reason = AdminRoles.All.Contains(entry.Name, StringComparer.OrdinalIgnoreCase)
                    ? "内置角色不能删除。"
                    : count > 0 ? $"仍分配给 {count} 个用户，不能删除。" : null;
                changes.Add(new(entry.Name, "delete", null, null, entry, null, reason, null));
            }
        }

        return new AccessTransferPreview(Hash(current), changes);
    }

    private async Task<Snapshot> LoadAsync(string kind, CancellationToken cancellationToken)
    {
        if (kind == "permissions")
        {
            var rows = await db.Permissions.AsNoTracking().OrderBy(x => x.Code)
                .Select(x => new TransferPermission(x.Code, x.Name, x.Description, x.IsEnabled))
                .ToListAsync(cancellationToken);
            var exactGrants = await db.RolePermissions.AsNoTracking()
                .Select(x => new { x.PermissionCode, x.RoleName }).ToListAsync(cancellationToken);
            var grantRoles = exactGrants.GroupBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key,
                    x => x.Select(y => y.RoleName).Order(StringComparer.Ordinal).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            return new(new(Format, Version, kind, rows, null),
                new(StringComparer.OrdinalIgnoreCase), new(StringComparer.Ordinal),
                grantRoles);
        }

        var roles = await roleManager.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var grants = await db.RolePermissions.AsNoTracking().ToListAsync(cancellationToken);
        var users = await db.UserRoles.GroupBy(x => x.RoleId)
            .Select(x => new { RoleId = x.Key, Count = x.Count() }).ToListAsync(cancellationToken);
        var exported = roles.Select(role => new TransferRole(role.Name!, grants
            .Where(x => string.Equals(x.RoleName, role.Name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.PermissionCode).Order(StringComparer.Ordinal).ToArray())).ToArray();
        var counts = roles.ToDictionary(x => x.Name!, x =>
            users.FirstOrDefault(y => y.RoleId == x.Id)?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
        var known = await db.Permissions.AsNoTracking().Select(x => x.Code).ToListAsync(cancellationToken);
        return new(new(Format, Version, kind, null, exported), counts,
            known.ToHashSet(StringComparer.Ordinal), new(StringComparer.OrdinalIgnoreCase));
    }

    private static void ValidateDocument(AccessTransferDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateKind(document.Kind);
        if (document.Format != Format || document.Version != Version)
            throw new ArgumentException("文件格式或版本不受支持。");
        if (document.Kind == "permissions")
        {
            if (document.Permissions is null || document.Roles is not null)
                throw new ArgumentException("权限文件结构不正确。");
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in document.Permissions)
            {
                if (item is null || item.Code is not { Length: <= 100 } ||
                    !AdminPermissions.IsCatalogCode(item.Code) || !codes.Add(item.Code) ||
                    string.IsNullOrWhiteSpace(item.Name) || item.Name != item.Name.Trim() ||
                    item.Name.Length > 200 || item.Description?.Length > 500)
                    throw new ArgumentException("权限文件包含重复或无效的权限码、名称或说明。");
            }
        }
        else
        {
            if (document.Roles is null || document.Permissions is not null)
                throw new ArgumentException("角色文件结构不正确。");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var role in document.Roles)
            {
                if (role is null || string.IsNullOrWhiteSpace(role.Name) ||
                    role.Name != role.Name.Trim() || role.Name.Length > 100 ||
                    !names.Add(role.Name) || role.Permissions is null)
                    throw new ArgumentException("角色文件包含重复或无效的角色名称。");
                var codes = new HashSet<string>(StringComparer.Ordinal);
                if (role.Permissions.Any(code => code is not { Length: <= 100 } ||
                    !AdminPermissions.IsGrantCode(code) || !codes.Add(code)))
                    throw new ArgumentException($"角色 {role.Name} 包含重复或无效的授权码。");
            }
        }
    }

    private static string ValidateKind(string kind) => kind switch
    {
        "permissions" or "roles" => kind,
        _ => throw new ArgumentException("仅支持 permissions 或 roles 文件。")
    };

    private static string Hash(Snapshot snapshot) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            snapshot.Document,
            UserCounts = snapshot.UserCounts.OrderBy(x => x.Key, StringComparer.Ordinal),
            ExactGrantRoles = snapshot.ExactGrantRoles.OrderBy(x => x.Key, StringComparer.Ordinal)
        }, JsonOptions))));

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private sealed record Snapshot(
        AccessTransferDocument Document,
        Dictionary<string, int> UserCounts,
        HashSet<string> KnownCodes,
        Dictionary<string, string[]> ExactGrantRoles);
}

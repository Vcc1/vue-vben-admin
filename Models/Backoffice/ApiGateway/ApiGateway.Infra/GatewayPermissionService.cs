using ApiGateway.App;
using ApiGateway.Domain;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Infra;

internal sealed class GatewayPermissionService(AdminDbContext db) : IGatewayPermissionService
{
    public async Task<IReadOnlyList<GatewayPermissionItem>> ListPermissionsAsync(Guid? moduleId, CancellationToken ct)
    {
        var query = db.GatewayPermissions.AsNoTracking().AsQueryable();
        if (moduleId.HasValue) query = query.Where(p => p.ModuleId == moduleId.Value);

        var permissions = await query.OrderBy(p => p.RoleName).ThenBy(p => p.CreatedAtUtc).ToListAsync(ct);

        // Batch-load module names + endpoint specs
        var moduleIds = permissions.Select(p => p.ModuleId).Distinct().ToList();
        var moduleNames = await db.GatewayModules.AsNoTracking()
            .Where(m => moduleIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var endpointIds = permissions.Where(p => p.EndpointId.HasValue).Select(p => p.EndpointId!.Value).Distinct().ToList();
        var endpointSpecs = endpointIds.Count > 0
            ? await db.GatewayEndpoints.AsNoTracking()
                .Where(e => endpointIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => $"{e.HttpMethod} {e.Path}", ct)
            : new Dictionary<Guid, string>();

        return permissions.Select(p => new GatewayPermissionItem(
            p.Id, p.RoleName, p.ModuleId,
            moduleNames.GetValueOrDefault(p.ModuleId, "?"),
            p.EndpointId,
            p.EndpointId.HasValue ? endpointSpecs.GetValueOrDefault(p.EndpointId.Value) : null,
            p.IsEnabled, p.CreatedAtUtc)).ToList();
    }

    public async Task<GatewayPermissionItem> GrantAsync(CreateGatewayPermissionRequest request, CancellationToken ct)
    {
        var module = await db.GatewayModules.AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.ModuleId, ct)
            ?? throw new KeyNotFoundException("Module not found.");

        var entity = new GatewayPermission
        {
            Id = Guid.CreateVersion7(),
            RoleName = request.RoleName.Trim(),
            ModuleId = request.ModuleId,
            EndpointId = request.EndpointId,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.GatewayPermissions.Add(entity);
        await db.SaveChangesAsync(ct);

        string? endpointSpec = null;
        if (request.EndpointId.HasValue)
        {
            var ep = await db.GatewayEndpoints.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.EndpointId.Value, ct);
            if (ep is not null) endpointSpec = $"{ep.HttpMethod} {ep.Path}";
        }

        return new GatewayPermissionItem(
            entity.Id, entity.RoleName, entity.ModuleId, module.Name,
            entity.EndpointId, endpointSpec, entity.IsEnabled, entity.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<GatewayPermissionItem>> BatchGrantAsync(BatchGrantPermissionRequest request, CancellationToken ct)
    {
        var moduleIds = request.ModuleIds.Distinct().ToList();
        var modules = await db.GatewayModules.AsNoTracking()
            .Where(m => moduleIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        var results = new List<GatewayPermissionItem>();
        foreach (var moduleId in moduleIds)
        {
            if (!modules.ContainsKey(moduleId)) continue;

            var exists = await db.GatewayPermissions.AnyAsync(
                p => p.RoleName == request.RoleName && p.ModuleId == moduleId && p.EndpointId == null, ct);
            if (exists) continue;

            var entity = new GatewayPermission
            {
                Id = Guid.CreateVersion7(),
                RoleName = request.RoleName.Trim(),
                ModuleId = moduleId,
                EndpointId = null, // Module-level wildcard
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.GatewayPermissions.Add(entity);
            results.Add(new GatewayPermissionItem(
                entity.Id, entity.RoleName, entity.ModuleId, modules[moduleId],
                null, null, entity.IsEnabled, entity.CreatedAtUtc));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    public async Task RevokeAsync(Guid permissionId, CancellationToken ct)
    {
        var entity = await db.GatewayPermissions.FindAsync([permissionId], ct);
        if (entity is null) return;
        db.GatewayPermissions.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetEnabledAsync(Guid permissionId, bool isEnabled, CancellationToken ct)
    {
        var entity = await db.GatewayPermissions.FindAsync([permissionId], ct)
            ?? throw new KeyNotFoundException("Permission not found.");
        entity.IsEnabled = isEnabled;
        await db.SaveChangesAsync(ct);
    }
}

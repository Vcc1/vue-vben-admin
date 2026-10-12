using ApiGateway.App;
using ApiGateway.Domain;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Infra;

internal sealed class GatewayModuleService(AdminDbContext db) : IGatewayModuleService
{
    public async Task<IReadOnlyList<GatewayModuleItem>> ListModulesAsync(Guid apiId, CancellationToken ct)
    {
        var modules = await db.GatewayModules.AsNoTracking()
            .Where(m => m.ApiId == apiId).OrderBy(m => m.PathPrefix).ToListAsync(ct);

        var moduleIds = modules.Select(m => m.Id).ToList();
        var endpointCounts = moduleIds.Count > 0
            ? await db.GatewayEndpoints.AsNoTracking()
                .Where(e => moduleIds.Contains(e.ModuleId))
                .GroupBy(e => e.ModuleId).Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct)
            : new Dictionary<Guid, int>();

        return modules.Select(m => new GatewayModuleItem(
            m.Id, m.ApiId, m.Name, m.PathPrefix,
            HeaderHelper.ParseHeaders(m.HeadersJson),
            endpointCounts.GetValueOrDefault(m.Id, 0),
            m.IsEnabled, m.CreatedAtUtc)).ToList();
    }

    public async Task<GatewayModuleItem> CreateModuleAsync(Guid apiId, CreateGatewayModuleRequest request, CancellationToken ct)
    {
        // Verify parent API exists
        var apiExists = await db.GatewayApis.AnyAsync(a => a.Id == apiId, ct);
        if (!apiExists) throw new KeyNotFoundException($"API '{apiId}' not found.");

        var module = new GatewayModule
        {
            Id = Guid.CreateVersion7(),
            ApiId = apiId,
            Name = request.Name.Trim(),
            PathPrefix = request.PathPrefix.TrimEnd('/'),
            HeadersJson = HeaderHelper.SerializeHeaders(request.Headers),
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.GatewayModules.Add(module);
        await db.SaveChangesAsync(ct);

        return new GatewayModuleItem(
            module.Id, module.ApiId, module.Name, module.PathPrefix,
            HeaderHelper.ParseHeaders(module.HeadersJson),
            0, module.IsEnabled, module.CreatedAtUtc);
    }

    public async Task<GatewayModuleItem> UpdateModuleAsync(Guid moduleId, UpdateGatewayModuleRequest request, CancellationToken ct)
    {
        var module = await db.GatewayModules.FindAsync([moduleId], ct)
            ?? throw new KeyNotFoundException("Module not found.");

        module.Name = request.Name.Trim();
        module.PathPrefix = request.PathPrefix.TrimEnd('/');
        module.HeadersJson = HeaderHelper.SerializeHeaders(request.Headers);
        await db.SaveChangesAsync(ct);

        var endpointCount = await db.GatewayEndpoints.CountAsync(e => e.ModuleId == moduleId, ct);
        return new GatewayModuleItem(
            module.Id, module.ApiId, module.Name, module.PathPrefix,
            HeaderHelper.ParseHeaders(module.HeadersJson),
            endpointCount, module.IsEnabled, module.CreatedAtUtc);
    }

    public async Task DeleteModuleAsync(Guid moduleId, CancellationToken ct)
    {
        var module = await db.GatewayModules.FindAsync([moduleId], ct);
        if (module is null) return;
        db.GatewayModules.Remove(module); // Cascade deletes endpoints
        await db.SaveChangesAsync(ct);
    }
}

using ApiGateway.App;
using ApiGateway.Domain;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Infra;

internal sealed class GatewayApiService(
    AdminDbContext db,
    ILogger<GatewayApiService> logger) : IGatewayApiService
{
    // ──────────────── API CRUD ────────────────

    public async Task<IReadOnlyList<GatewayApiItem>> ListApisAsync(CancellationToken ct)
    {
        var apis = await db.GatewayApis.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var moduleCounts = await db.GatewayModules.AsNoTracking()
            .GroupBy(m => m.ApiId).Select(g => new { ApiId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ApiId, x => x.Count, ct);

        return apis.Select(a => new GatewayApiItem(
            a.Id, a.Name, a.ApiKey, a.BaseUrl, a.IsEnabled,
            moduleCounts.GetValueOrDefault(a.Id, 0), a.CreatedAtUtc, a.UpdatedAtUtc)).ToList();
    }

    public async Task<GatewayApiDetail> GetApiDetailAsync(Guid apiId, CancellationToken ct)
    {
        var api = await db.GatewayApis.AsNoTracking().SingleOrDefaultAsync(x => x.Id == apiId, ct)
            ?? throw new KeyNotFoundException("API not found.");

        var modules = await db.GatewayModules.AsNoTracking()
            .Where(m => m.ApiId == apiId).OrderBy(m => m.PathPrefix).ToListAsync(ct);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var endpointCounts = await db.GatewayEndpoints.AsNoTracking()
            .Where(e => moduleIds.Contains(e.ModuleId))
            .GroupBy(e => e.ModuleId).Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModuleId, x => x.Count, ct);

        var moduleItems = modules.Select(m => new GatewayModuleItem(
            m.Id, m.ApiId, m.Name, m.PathPrefix,
            HeaderHelper.ParseHeaders(m.HeadersJson),
            endpointCounts.GetValueOrDefault(m.Id, 0), m.IsEnabled, m.CreatedAtUtc)).ToList();

        return new GatewayApiDetail(
            api.Id, api.Name, api.ApiKey, api.BaseUrl,
            HeaderHelper.ParseHeaders(api.HeadersJson),
            api.IsEnabled, moduleItems, api.CreatedAtUtc, api.UpdatedAtUtc);
    }

    public async Task<GatewayApiItem> CreateApiAsync(CreateGatewayApiRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entity = new GatewayApi
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            ApiKey = GenerateApiKey(request.Name),
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            HeadersJson = HeaderHelper.SerializeHeaders(request.Headers),
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.GatewayApis.Add(entity);
        await db.SaveChangesAsync(ct);
        return new GatewayApiItem(entity.Id, entity.Name, entity.ApiKey, entity.BaseUrl,
            entity.IsEnabled, 0, entity.CreatedAtUtc, entity.UpdatedAtUtc);
    }

    public async Task<GatewayApiItem> UpdateApiAsync(Guid apiId, UpdateGatewayApiRequest request, CancellationToken ct)
    {
        var entity = await db.GatewayApis.FindAsync([apiId], ct)
            ?? throw new KeyNotFoundException("API not found.");
        entity.Name = request.Name.Trim();
        entity.ApiKey = GenerateApiKey(request.Name);
        entity.BaseUrl = request.BaseUrl.TrimEnd('/');
        entity.HeadersJson = HeaderHelper.SerializeHeaders(request.Headers);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var moduleCount = await db.GatewayModules.CountAsync(m => m.ApiId == apiId, ct);
        return new GatewayApiItem(entity.Id, entity.Name, entity.ApiKey, entity.BaseUrl,
            entity.IsEnabled, moduleCount, entity.CreatedAtUtc, entity.UpdatedAtUtc);
    }

    public async Task DeleteApiAsync(Guid apiId, CancellationToken ct)
    {
        var entity = await db.GatewayApis.FindAsync([apiId], ct);
        if (entity is null) return;
        db.GatewayApis.Remove(entity); // Cascade deletes modules + endpoints
        await db.SaveChangesAsync(ct);
    }

    public async Task SetApiEnabledAsync(Guid apiId, bool isEnabled, CancellationToken ct)
    {
        var entity = await db.GatewayApis.FindAsync([apiId], ct)
            ?? throw new KeyNotFoundException("API not found.");
        entity.IsEnabled = isEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ──────────────── Import Diff & Apply ────────────────

    public async Task<ImportDiffResult> GetImportDiffAsync(
        Guid apiId, ImportDiffRequest request, CancellationToken ct)
    {
        var apiExists = await db.GatewayApis.AnyAsync(a => a.Id == apiId, ct);
        if (!apiExists) throw new KeyNotFoundException("API not found.");

        // Load existing modules + endpoints for this API
        var existingModules = await db.GatewayModules.AsNoTracking()
            .Where(m => m.ApiId == apiId).ToListAsync(ct);
        var existingModuleIds = existingModules.Select(m => m.Id).ToList();
        var existingEndpoints = existingModuleIds.Count > 0
            ? await db.GatewayEndpoints.AsNoTracking()
                .Where(e => existingModuleIds.Contains(e.ModuleId)).ToListAsync(ct)
            : [];

        // Group endpoints by module for quick lookup
        var endpointsByModule = existingEndpoints.GroupBy(e => e.ModuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Permission counts per endpoint
        var allEndpointIds = existingEndpoints.Select(e => e.Id).ToList();
        var permCounts = allEndpointIds.Count > 0
            ? await db.GatewayPermissions.AsNoTracking()
                .Where(p => p.EndpointId.HasValue && allEndpointIds.Contains(p.EndpointId.Value))
                .GroupBy(p => p.EndpointId!.Value)
                .Select(g => new { EndpointId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.EndpointId, x => x.Count, ct)
            : new Dictionary<Guid, int>();

        var result = new List<ModuleDiffItem>();

        foreach (var incoming in request.Modules)
        {
            // Match by module name within this API
            var existingModule = existingModules
                .SingleOrDefault(m => m.Name.Equals(incoming.Name, StringComparison.OrdinalIgnoreCase));

            if (existingModule is null)
            {
                // New module — all endpoints are new
                var newEndpoints = incoming.Endpoints.Select(ep => new EndpointDiffItem(
                    "Add", null, ep.HttpMethod.ToUpperInvariant(), ep.Path,
                    ep.OperationId, ep.Summary, null)).ToList();

                result.Add(new ModuleDiffItem(
                    "Add", null, incoming.Name, incoming.PathPrefix,
                    incoming.Headers, newEndpoints));
            }
            else
            {
                // Existing module — compare endpoints
                var moduleEndpoints = endpointsByModule.GetValueOrDefault(existingModule.Id) ?? [];
                var endpointDiffs = new List<EndpointDiffItem>();

                foreach (var ep in incoming.Endpoints)
                {
                    var method = ep.HttpMethod.ToUpperInvariant();
                    var existingEp = moduleEndpoints
                        .SingleOrDefault(e => e.HttpMethod == method
                            && e.Path.Equals(ep.Path, StringComparison.OrdinalIgnoreCase));

                    if (existingEp is null)
                    {
                        endpointDiffs.Add(new EndpointDiffItem(
                            "Add", null, method, ep.Path,
                            ep.OperationId, ep.Summary, null));
                    }
                    else
                    {
                        // Check if anything changed
                        var changed = existingEp.OperationId != ep.OperationId
                            || existingEp.Summary != ep.Summary;
                        if (changed)
                        {
                            endpointDiffs.Add(new EndpointDiffItem(
                                "Modify", existingEp.Id, method, ep.Path,
                                ep.OperationId, ep.Summary,
                                permCounts.GetValueOrDefault(existingEp.Id, 0)));
                        }
                        // If unchanged, skip (no diff to show)
                    }
                }

                // Only include module if there are endpoint changes or module-level changes
                if (endpointDiffs.Count > 0
                    || existingModule.PathPrefix != incoming.PathPrefix
                    || existingModule.HeadersJson != HeaderHelper.SerializeHeaders(incoming.Headers))
                {
                    result.Add(new ModuleDiffItem(
                        "Modify", existingModule.Id, incoming.Name, incoming.PathPrefix,
                        incoming.Headers, endpointDiffs));
                }
            }
        }

        return new ImportDiffResult(result);
    }

    public async Task ApplyImportAsync(Guid apiId, ApplyImportRequest request, CancellationToken ct)
    {
        var api = await db.GatewayApis.FindAsync([apiId], ct)
            ?? throw new KeyNotFoundException("API not found.");

        // 1. Delete modules marked for removal
        if (request.DeleteModuleIds.Count > 0)
        {
            var toDelete = await db.GatewayModules
                .Where(m => m.ApiId == apiId && request.DeleteModuleIds.Contains(m.Id))
                .ToListAsync(ct);
            db.GatewayModules.RemoveRange(toDelete);
        }

        // 2. Process each module
        foreach (var moduleItem in request.Modules)
        {
            GatewayModule module;

            if (moduleItem.Action == "Add")
            {
                module = new GatewayModule
                {
                    Id = Guid.CreateVersion7(),
                    ApiId = apiId,
                    Name = moduleItem.Name.Trim(),
                    PathPrefix = moduleItem.PathPrefix.TrimEnd('/'),
                    HeadersJson = HeaderHelper.SerializeHeaders(moduleItem.Headers),
                    IsEnabled = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.GatewayModules.Add(module);
            }
            else // Modify
            {
                module = await db.GatewayModules.FindAsync([moduleItem.ExistingModuleId!.Value], ct)
                    ?? throw new KeyNotFoundException($"Module '{moduleItem.Name}' not found.");
                module.Name = moduleItem.Name.Trim();
                module.PathPrefix = moduleItem.PathPrefix.TrimEnd('/');
                module.HeadersJson = HeaderHelper.SerializeHeaders(moduleItem.Headers);
            }

            // 3. Process endpoints within module
            foreach (var ep in moduleItem.Endpoints)
            {
                if (ep.Action == "Add")
                {
                    db.GatewayEndpoints.Add(new GatewayEndpoint
                    {
                        Id = Guid.CreateVersion7(),
                        ModuleId = module.Id,
                        HttpMethod = ep.HttpMethod.ToUpperInvariant(),
                        Path = ep.Path.Trim(),
                        OperationId = ep.OperationId?.Trim(),
                        Summary = ep.Summary?.Trim(),
                        IsEnabled = true,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
                else if (ep.Action == "Modify" && ep.ExistingEndpointId.HasValue)
                {
                    var existingEp = await db.GatewayEndpoints.FindAsync([ep.ExistingEndpointId.Value], ct);
                    if (existingEp is not null)
                    {
                        existingEp.HttpMethod = ep.HttpMethod.ToUpperInvariant();
                        existingEp.Path = ep.Path.Trim();
                        existingEp.OperationId = ep.OperationId?.Trim();
                        existingEp.Summary = ep.Summary?.Trim();
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ──────────────── Helpers ────────────────

    private static string GenerateApiKey(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
}

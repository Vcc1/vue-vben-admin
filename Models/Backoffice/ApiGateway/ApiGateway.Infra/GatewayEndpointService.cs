using ApiGateway.App;
using ApiGateway.Domain;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Infra;

internal sealed class GatewayEndpointService(AdminDbContext db) : IGatewayEndpointService
{
    public async Task<IReadOnlyList<GatewayEndpointItem>> ListEndpointsAsync(Guid moduleId, CancellationToken ct)
    {
        var endpoints = await db.GatewayEndpoints.AsNoTracking()
            .Where(e => e.ModuleId == moduleId).OrderBy(e => e.Path).ToListAsync(ct);

        return endpoints.Select(e => new GatewayEndpointItem(
            e.Id, e.ModuleId, e.HttpMethod, e.Path,
            e.OperationId, e.Summary, e.IsEnabled, e.CreatedAtUtc)).ToList();
    }

    public async Task<GatewayEndpointItem> CreateEndpointAsync(Guid moduleId, CreateGatewayEndpointRequest request, CancellationToken ct)
    {
        var moduleExists = await db.GatewayModules.AnyAsync(m => m.Id == moduleId, ct);
        if (!moduleExists) throw new KeyNotFoundException($"Module '{moduleId}' not found.");

        var endpoint = new GatewayEndpoint
        {
            Id = Guid.CreateVersion7(),
            ModuleId = moduleId,
            HttpMethod = request.HttpMethod.ToUpperInvariant(),
            Path = request.Path.Trim(),
            OperationId = request.OperationId?.Trim(),
            Summary = request.Summary?.Trim(),
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.GatewayEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);

        return new GatewayEndpointItem(
            endpoint.Id, endpoint.ModuleId, endpoint.HttpMethod, endpoint.Path,
            endpoint.OperationId, endpoint.Summary, endpoint.IsEnabled, endpoint.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<GatewayEndpointItem>> BatchCreateEndpointsAsync(
        Guid moduleId, IReadOnlyList<CreateGatewayEndpointRequest> requests, CancellationToken ct)
    {
        var moduleExists = await db.GatewayModules.AnyAsync(m => m.Id == moduleId, ct);
        if (!moduleExists) throw new KeyNotFoundException($"Module '{moduleId}' not found.");

        var results = new List<GatewayEndpointItem>();
        foreach (var request in requests)
        {
            var endpoint = new GatewayEndpoint
            {
                Id = Guid.CreateVersion7(),
                ModuleId = moduleId,
                HttpMethod = request.HttpMethod.ToUpperInvariant(),
                Path = request.Path.Trim(),
                OperationId = request.OperationId?.Trim(),
                Summary = request.Summary?.Trim(),
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.GatewayEndpoints.Add(endpoint);
            results.Add(new GatewayEndpointItem(
                endpoint.Id, endpoint.ModuleId, endpoint.HttpMethod, endpoint.Path,
                endpoint.OperationId, endpoint.Summary, endpoint.IsEnabled, endpoint.CreatedAtUtc));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    public async Task DeleteEndpointAsync(Guid endpointId, CancellationToken ct)
    {
        var endpoint = await db.GatewayEndpoints.FindAsync([endpointId], ct);
        if (endpoint is null) return;
        db.GatewayEndpoints.Remove(endpoint);
        await db.SaveChangesAsync(ct);
    }
}

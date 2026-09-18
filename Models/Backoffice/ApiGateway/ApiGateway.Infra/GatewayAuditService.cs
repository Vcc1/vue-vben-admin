using ApiGateway.App;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Infra;

internal sealed class GatewayAuditService(AdminDbContext db) : IGatewayAuditService
{
    public async Task<IReadOnlyList<GatewayAuditLogItem>> QueryAsync(
        Guid? apiId, Guid? moduleId, string? actorName, int take, CancellationToken ct)
    {
        var query = db.GatewayAuditLogs.AsNoTracking().AsQueryable();
        if (apiId.HasValue) query = query.Where(l => l.ApiId == apiId.Value);
        if (moduleId.HasValue) query = query.Where(l => l.ModuleId == moduleId.Value);
        if (!string.IsNullOrWhiteSpace(actorName))
            query = query.Where(l => l.ActorName.Contains(actorName));

        var logs = await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(ct);

        return logs.Select(l => new GatewayAuditLogItem(
            l.Id, l.ApiId, l.ApiName, l.ModuleId, l.ModuleName, l.EndpointId,
            l.ActorType, l.ActorId, l.ActorName,
            l.HttpMethod, l.Path, l.StatusCode, l.DurationMs,
            l.OccurredAtUtc, l.IpAddress)).ToList();
    }
}

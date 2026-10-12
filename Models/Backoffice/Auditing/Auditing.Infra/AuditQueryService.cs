using Auditing.App;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;

namespace Auditing.Infra;

internal sealed class AuditQueryService(AdminDbContext db) : IAuditQueryService
{
    public async Task<IReadOnlyList<RequestAuditLogItem>> QueryRequestLogsAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken)
    {
        var query = db.RequestAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.ActorName))
            query = query.Where(l => l.ActorName.Contains(filter.ActorName));
        if (!string.IsNullOrWhiteSpace(filter.Path))
            query = query.Where(l => l.Path.Contains(filter.Path));
        if (!string.IsNullOrWhiteSpace(filter.HttpMethod))
            query = query.Where(l => l.HttpMethod == filter.HttpMethod);
        if (!string.IsNullOrWhiteSpace(filter.Feature))
            query = query.Where(l => l.FeatureName != null && l.FeatureName.Contains(filter.Feature));
        if (filter.FromUtc.HasValue)
            query = query.Where(l => l.OccurredAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue)
            query = query.Where(l => l.OccurredAtUtc <= filter.ToUtc.Value);

        return await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(Math.Clamp(filter.Take, 1, 1000))
            .Select(l => new RequestAuditLogItem(
                l.Id, l.ActorType, l.ActorId, l.ActorName, l.AuthenticationType,
                l.IpAddress, l.UserAgent, l.CorrelationId,
                l.HttpMethod, l.Path, l.QueryString, l.ControllerName, l.ActionName,
                l.FeatureName, l.OperationName,
                l.StatusCode, l.DurationMs,
                l.RequestBody, l.IsRequestBodyTruncated,
                l.ResponseBody, l.IsResponseBodyTruncated,
                l.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecurityEventItem>> QuerySecurityEventsAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken)
    {
        var query = db.SecurityEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.ActorName))
            query = query.Where(l => l.ActorName.Contains(filter.ActorName));
        if (!string.IsNullOrWhiteSpace(filter.EventType))
            query = query.Where(l => l.EventType.Contains(filter.EventType));
        if (!string.IsNullOrWhiteSpace(filter.Severity))
            query = query.Where(l => l.Severity == filter.Severity);
        if (filter.FromUtc.HasValue)
            query = query.Where(l => l.OccurredAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue)
            query = query.Where(l => l.OccurredAtUtc <= filter.ToUtc.Value);

        return await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(Math.Clamp(filter.Take, 1, 1000))
            .Select(l => new SecurityEventItem(
                l.Id, l.EventType, l.Severity, l.Summary,
                l.ActorType, l.ActorId, l.ActorName, l.AuthenticationType,
                l.IpAddress, l.UserAgent, l.CorrelationId,
                l.DetailJson, l.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }
}

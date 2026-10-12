using AdminIdentity.App;
using Auditing.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Auditing;

namespace Auditing.Infra;

internal sealed class RequestAuditStore(
    AdminDbContext dbContext,
    ICurrentActorAccessor actorAccessor,
    TimeProvider timeProvider)
    : IRequestAuditStore
{
    public async Task WriteAsync(RequestAuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var actor = actorAccessor.Current;
        dbContext.RequestAuditLogs.Add(new RequestAuditLog
        {
            ActorType = actor.ActorType,
            ActorId = actor.ActorId,
            ActorName = actor.ActorName,
            AuthenticationType = actor.AuthenticationType,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            CorrelationId = actor.CorrelationId,
            HttpMethod = entry.HttpMethod,
            Path = entry.Path,
            QueryString = entry.QueryString,
            ControllerName = entry.ControllerName,
            ActionName = entry.ActionName,
            FeatureName = entry.FeatureName,
            OperationName = entry.OperationName,
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            RequestBody = entry.RequestBody,
            IsRequestBodyTruncated = entry.IsRequestBodyTruncated,
            ResponseBody = entry.ResponseBody,
            IsResponseBodyTruncated = entry.IsResponseBodyTruncated,
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

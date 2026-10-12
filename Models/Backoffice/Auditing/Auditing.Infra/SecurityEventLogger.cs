using System.Text.Encodings.Web;
using System.Text.Json;
using AdminIdentity.App;
using Auditing.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Auditing;

namespace Auditing.Infra;

internal sealed class SecurityEventLogger(
    AdminDbContext dbContext,
    ICurrentActorAccessor actorAccessor,
    TimeProvider timeProvider)
    : ISecurityEventLogger
{
    private static readonly JsonSerializerOptions DetailJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task LogAsync(SecurityEvent securityEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        var actor = actorAccessor.Current;
        dbContext.SecurityEvents.Add(new SecurityEventLog
        {
            EventType = securityEvent.EventType,
            Severity = securityEvent.Severity,
            Summary = securityEvent.Summary,
            ActorType = actor.ActorType,
            ActorId = securityEvent.ActorId ?? actor.ActorId,
            ActorName = securityEvent.ActorName ?? actor.ActorName,
            AuthenticationType = actor.AuthenticationType,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            CorrelationId = actor.CorrelationId,
            DetailJson = securityEvent.Detail is null
                ? null
                : JsonSerializer.Serialize(securityEvent.Detail, DetailJsonOptions),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

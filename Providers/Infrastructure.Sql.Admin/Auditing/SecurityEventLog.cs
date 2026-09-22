namespace Infrastructure.Sql.Admin.Auditing;

public sealed class SecurityEventLog
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string AuthenticationType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

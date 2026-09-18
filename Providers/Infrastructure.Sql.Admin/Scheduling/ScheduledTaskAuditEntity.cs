namespace Infrastructure.Sql.Admin.Scheduling;

public sealed class ScheduledTaskAuditEntity
{
    public long Id { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? RunId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string AuthenticationType { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
}

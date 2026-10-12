namespace Infrastructure.Sql.Admin.Scheduling;

public sealed class ScheduledTaskRunEntity
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid RootRunId { get; set; }
    public int AttemptNumber { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime QueuedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public DateTime? CancellationRequestedAtUtc { get; set; }
    public string? TriggeredByActorType { get; set; }
    public string? TriggeredByActorId { get; set; }
    public string? TriggeredByActorName { get; set; }
    public string? AuthenticationType { get; set; }
    public string? HostInstanceId { get; set; }
    public string? QuartzFireInstanceId { get; set; }
    public string DefinitionSnapshotJson { get; set; } = "{}";
    public string? ResultSummary { get; set; }
    public string? ErrorSummary { get; set; }
    public string LogRelativePath { get; set; } = string.Empty;
    public long LogLengthBytes { get; set; }
    public bool IsLogTruncated { get; set; }
    public DateTime? LogDeletedAtUtc { get; set; }

    public ScheduledTaskEntity Task { get; set; } = null!;
}

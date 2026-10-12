namespace Infrastructure.Sql.Admin.Scheduling;

public sealed class ScheduledTaskEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    public string MisfirePolicy { get; set; } = "DoNothing";
    public bool IsEnabled { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public int RetryIntervalSeconds { get; set; }
    public bool RetryOnTimeout { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public string CreatedByActorId { get; set; } = string.Empty;
    public string CreatedByActorName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string UpdatedByActorId { get; set; } = string.Empty;
    public string UpdatedByActorName { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

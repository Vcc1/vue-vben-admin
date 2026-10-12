namespace Infrastructure.Sql.Admin.Notification;

public sealed class NotificationEntity
{
    public Guid Id { get; set; }
    public Guid NotificationTypeId { get; set; }
    public NotificationTypeEntity NotificationType { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public string? LinkUrl { get; set; }
    public string Scope { get; set; } = "all";
    public string TargetIdsJson { get; set; } = "[]";
    public string Source { get; set; } = "manual";
    public string? SourceModule { get; set; }
    public string? SourceEventKey { get; set; }
    public string CreatedByActorId { get; set; } = string.Empty;
    public string CreatedByActorName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

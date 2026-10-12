namespace Infrastructure.Sql.Admin.Notification;

public sealed class NotificationTypeEntity
{
    public Guid Id { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AllowedChannelsJson { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;
    public bool IsSystem { get; set; }
    public string CreatedByActorId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string UpdatedByActorId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

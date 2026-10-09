namespace Infrastructure.Sql.Admin.Notification;

public sealed class UserNotificationPreferenceEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid NotificationTypeId { get; set; }
    public NotificationTypeEntity NotificationType { get; set; } = null!;
    public string EnabledChannelsJson { get; set; } = "[]";
    public DateTime UpdatedAtUtc { get; set; }
}

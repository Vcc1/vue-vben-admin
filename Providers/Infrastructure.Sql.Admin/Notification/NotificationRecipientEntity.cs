namespace Infrastructure.Sql.Admin.Notification;

public sealed class NotificationRecipientEntity
{
    public long Id { get; set; }
    public Guid NotificationId { get; set; }
    public NotificationEntity Notification { get; set; } = null!;
    public Guid UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public string DeliveredChannelsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
}

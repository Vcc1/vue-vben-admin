namespace Notification.Contracts;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationDraft draft, string? source = null, CancellationToken ct = default);
}

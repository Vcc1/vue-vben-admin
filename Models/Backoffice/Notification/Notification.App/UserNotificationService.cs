namespace Notification.App;

public sealed class UserNotificationService(
    INotificationStore store,
    ICurrentUserIdAccessor userIdAccessor)
{
    public async Task<UserNotificationPage> GetNotificationsAsync(NotificationQuery query, CancellationToken ct)
    {
        var userId = userIdAccessor.UserId;
        return await store.GetUserNotificationsAsync(userId, query, ct);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct)
    {
        var userId = userIdAccessor.UserId;
        return await store.GetUnreadCountAsync(userId, ct);
    }

    public async Task MarkAsReadAsync(IReadOnlyList<Guid> notificationIds, CancellationToken ct)
    {
        var userId = userIdAccessor.UserId;
        await store.MarkAsReadAsync(userId, notificationIds, ct);
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct)
    {
        var userId = userIdAccessor.UserId;
        await store.MarkAllAsReadAsync(userId, ct);
    }
}

public interface ICurrentUserIdAccessor
{
    Guid UserId { get; }
}

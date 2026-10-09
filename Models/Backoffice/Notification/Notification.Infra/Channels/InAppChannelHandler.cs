using Notification.App;
using Notification.Contracts;

namespace Notification.Infra.Channels;

internal sealed class InAppChannelHandler(INotificationHubPusher hubPusher) : INotificationChannelHandler
{
    public string ChannelCode => NotificationChannels.InApp;

    public async Task SendAsync(NotificationBrief notification, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var dto = new NotificationDto(
            notification.Id,
            0,
            notification.TypeCode,
            notification.TypeDisplayName,
            notification.Title,
            notification.Summary,
            notification.ContentJson,
            notification.LinkUrl,
            false,
            notification.CreatedAtUtc);
        await hubPusher.PushToUsersAsync(userIds, dto, ct);
    }
}

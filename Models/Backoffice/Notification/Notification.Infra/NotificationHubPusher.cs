using Microsoft.AspNetCore.SignalR;
using Notification.App;

namespace Notification.Infra;

internal sealed class NotificationHubPusher(
    IHubContext<NotificationHub> hubContext) : INotificationHubPusher
{
    public async Task PushToUsersAsync(IReadOnlyList<Guid> userIds, NotificationDto dto, CancellationToken ct)
    {
        foreach (var userId in userIds)
        {
            await hubContext.Clients.Group(userId.ToString())
                .SendAsync("ReceiveNotification", dto, ct);
        }
    }
}

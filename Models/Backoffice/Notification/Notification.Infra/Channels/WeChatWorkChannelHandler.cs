using Microsoft.Extensions.Logging;
using Notification.App;
using Notification.Contracts;

namespace Notification.Infra.Channels;

internal sealed class WeChatWorkChannelHandler(
    ILogger<WeChatWorkChannelHandler> logger) : INotificationChannelHandler
{
    public string ChannelCode => NotificationChannels.WeChat;

    public Task SendAsync(NotificationBrief notification, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        // TODO: 实现企微推送
        // 1. 将 userIds 映射为企微 userid（需要映射表或 AdminUser 扩展字段）
        // 2. 组装 WorkWechatTemplateMessageRequest
        // 3. 调用 IWorkWechatTemplateMessageApi.SendAsync(...)
        logger.LogInformation("WeChatWork channel handler called for notification {NotificationId}, users: {UserCount}",
            notification.Id, userIds.Count);
        return Task.CompletedTask;
    }
}

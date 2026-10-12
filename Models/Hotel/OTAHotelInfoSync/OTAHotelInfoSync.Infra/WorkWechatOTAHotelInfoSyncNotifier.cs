using Infrastructure.HTTP.WorkWechatAlert;
using Microsoft.Extensions.Options;
using OTAHotelInfoSync;
using OTAHotelInfoSync.Contracts;

namespace Infrastructure.OTAHotelInfoSync;

public sealed class WorkWechatOTAHotelInfoSyncNotifier : IOTAHotelInfoSyncNotifier
{
    const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss zzz";

    readonly IWorkWechatTemplateMessageApi _api;
    readonly IOptionsMonitor<OTAHotelInfoSyncOptions> _options;

    public WorkWechatOTAHotelInfoSyncNotifier(
        IWorkWechatTemplateMessageApi api,
        IOptionsMonitor<OTAHotelInfoSyncOptions> options)
    {
        _api = api;
        _options = options;
    }

    public Task SendFailureAsync(
        OTAHotelInfoSyncFailureNotification notification,
        CancellationToken cancellationToken = default)
    {
        var content = $"""
            【告警】OTAHotelInfoSync {notification.FailureType}连续失败

            操作：{notification.Operation}
            连续失败：{notification.ConsecutiveFailureCount} 次
            首次失败：{notification.FirstFailedAt.ToString(DateTimeFormat)}
            本次失败：{notification.FailedAt.ToString(DateTimeFormat)}
            异常：{notification.ErrorMessage}
            服务将继续重试，不会跳过本次同步。
            """;
        return SendAsync(content, cancellationToken);
    }

    public Task SendRecoveryAsync(
        OTAHotelInfoSyncRecoveryNotification notification,
        CancellationToken cancellationToken = default)
    {
        var content = $"""
            【恢复】OTAHotelInfoSync {notification.FailureType}已恢复

            操作：{notification.Operation}
            连续失败：{notification.ConsecutiveFailureCount} 次
            首次失败：{notification.FirstFailedAt.ToString(DateTimeFormat)}
            恢复时间：{notification.RecoveredAt.ToString(DateTimeFormat)}
            """;
        return SendAsync(content, cancellationToken);
    }

    Task SendAsync(string content, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var toUsers = options.AlertToUsers
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (toUsers.Length == 0)
        {
            throw new InvalidOperationException("OTAHotelInfoSync:AlertToUsers is empty.");
        }

        return _api.SendAsync(new WorkWechatTemplateMessageRequest
        {
            ToUsers = toUsers,
            Agentid = options.AlertAgentId,
            ToDept = null,
            ToTag = null,
            Urls = null,
            Content = content
        }, cancellationToken);
    }
}

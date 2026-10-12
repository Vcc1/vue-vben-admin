using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using Infrastructure.HTTP.WorkWechatAlert;
using Microsoft.Extensions.Options;

namespace HotelMatchingPipeline.Infra;

public sealed class WorkWechatHotelMatchVetoNotifier : IHotelMatchVetoNotifier
{
    const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss zzz";

    readonly IWorkWechatTemplateMessageApi _api;
    readonly IOptionsMonitor<HotelMatchVetoOptions> _options;

    public WorkWechatHotelMatchVetoNotifier(
        IWorkWechatTemplateMessageApi api,
        IOptionsMonitor<HotelMatchVetoOptions> options)
    {
        _api = api;
        _options = options;
    }

    public Task SendFailureAsync(
        HotelMatchVetoSyncItem veto,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var retryDelaySeconds = Math.Max(1, options.RetryDelaySeconds);
        var fastRetryFailureCount = Math.Max(1, options.FastRetryFailureCount);
        var fastRetryDelaySeconds = Math.Max(1, options.FastRetryDelaySeconds);
        var content = $"""
            【告警】酒店匹配否决 Redis 同步失败

            否决ID：{veto.Id}
            供应商酒店：{veto.SupId}_{veto.SHotelCode}
            MTHotelCode：{veto.MTHotelCode}
            操作人：{veto.Operator}
            原因：{veto.Reason}
            尝试次数：{veto.RedisSyncAttemptCount}
            首次失败：{veto.FirstRedisSyncFailureAt?.ToString(DateTimeFormat)}
            本次失败：{veto.LastRedisSyncAttemptAt?.ToString(DateTimeFormat)}
            异常：{veto.LastRedisSyncError}
            前 {fastRetryFailureCount} 次失败每 {fastRetryDelaySeconds} 秒重试，之后每 {retryDelaySeconds} 秒重试。
            """;
        return SendAsync(content, cancellationToken);
    }

    public Task SendRecoveryAsync(
        HotelMatchVetoSyncItem veto,
        CancellationToken cancellationToken)
    {
        var content = $"""
            【恢复】酒店匹配否决 Redis 同步已恢复

            否决ID：{veto.Id}
            供应商酒店：{veto.SupId}_{veto.SHotelCode}
            MTHotelCode：{veto.MTHotelCode}
            尝试次数：{veto.RedisSyncAttemptCount}
            首次失败：{veto.FirstRedisSyncFailureAt?.ToString(DateTimeFormat)}
            恢复时间：{veto.RedisSyncedAt?.ToString(DateTimeFormat)}
            """;
        return SendAsync(content, cancellationToken);
    }

    Task SendAsync(string content, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var recipients = options.AlertToUsers
            .Where(static user => !string.IsNullOrWhiteSpace(user))
            .Select(static user => user.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (recipients.Length == 0)
        {
            throw new InvalidOperationException("HotelMatchingVeto:AlertToUsers is empty.");
        }

        return _api.SendAsync(new WorkWechatTemplateMessageRequest
        {
            ToUsers = recipients,
            Agentid = options.AlertAgentId,
            ToDept = null,
            ToTag = null,
            Urls = null,
            Content = content
        }, cancellationToken);
    }
}

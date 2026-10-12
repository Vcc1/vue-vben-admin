using Infrastructure.HTTP.WorkWechatAlert;
using MetricsAndAlertsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.MetricsAndAlertsApp;

public sealed class WorkWechatAlertNotifier : IAlertNotifier
{
    readonly IWorkWechatTemplateMessageApi _api;
    readonly IOptionsMonitor<AlertNotificationOptions> _options;
    readonly ILogger<WorkWechatAlertNotifier> _logger;

    public WorkWechatAlertNotifier(
        IWorkWechatTemplateMessageApi api,
        IOptionsMonitor<AlertNotificationOptions> options,
        ILogger<WorkWechatAlertNotifier> logger)
    {
        _api = api;
        _options = options;
        _logger = logger;
    }

    public Task<NotifyResult> SendAlertAsync(AlertMessage message, CancellationToken cancellationToken = default)
    {
        return SendAsync(message, cancellationToken);
    }

    public Task<NotifyResult> SendRecoverAsync(AlertMessage message, CancellationToken cancellationToken = default)
    {
        return SendAsync(message, cancellationToken);
    }

    public Task<NotifyResult> SendMessageAsync(AlertMessage message, CancellationToken cancellationToken = default)
    {
        return SendAsync(message, cancellationToken);
    }

    async Task<NotifyResult> SendAsync(AlertMessage message, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var configuredUsers = message.ToUsers.Count > 0
            ? message.ToUsers
            : options.DefaultToUsers;

        var toUsers = configuredUsers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (toUsers.Length == 0)
        {
            return NotifyResult.Failure("AlertNotification:DefaultToUsers is empty.");
        }

        try
        {
            await _api.SendAsync(new WorkWechatTemplateMessageRequest
            {
                ToUsers = toUsers,
                Agentid = options.AgentId,
                ToDept = null,
                ToTag = null,
                Urls = null,
                Content = $"{message.Title}{Environment.NewLine}{Environment.NewLine}{message.Content}"
            }, cancellationToken);

            return NotifyResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send MetricsAndAlerts notification.");
            return NotifyResult.Failure(ex.Message);
        }
    }
}

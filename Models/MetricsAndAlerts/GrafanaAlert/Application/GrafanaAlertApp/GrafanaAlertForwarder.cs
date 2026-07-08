using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrafanaAlertApp;

public sealed class GrafanaAlertForwarder : IGrafanaAlertForwarder
{
    static readonly char[] UserSeparators = [',', ';', '|'];
    static readonly string[] AgentIdLabelKeys = ["agent_id", "agentid", "AgentId"];

    readonly IInternalAlertMessageSender _sender;
    readonly IOptionsMonitor<GrafanaAlertOptions> _options;
    readonly ILogger<GrafanaAlertForwarder> _logger;

    public GrafanaAlertForwarder(
        IInternalAlertMessageSender sender,
        IOptionsMonitor<GrafanaAlertOptions> options,
        ILogger<GrafanaAlertForwarder> logger)
    {
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    public async Task ForwardAsync(GrafanaWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var options = _options.CurrentValue;
        var toUsers = ResolveToUsers(payload, options);
        var agentId = ResolveAgentId(payload, options);
        var content = BuildContent(payload);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Grafana 告警内容为空。");
        }

        await _sender.SendAsync(new InternalAlertMessage
        {
            ToUsers = toUsers,
            AgentId = agentId,
            Content = content
        }, cancellationToken);

        _logger.LogInformation(
            "Grafana alert forwarded. Status={Status}, Alerts={AlertCount}, ToUsers={ToUsers}, AgentId={AgentId}",
            payload.Status,
            payload.Alerts.Count,
            string.Join(',', toUsers),
            agentId);
    }

    IReadOnlyList<string> ResolveToUsers(GrafanaWebhookPayload payload, GrafanaAlertOptions options)
    {
        var notifyUsers = payload.Alerts
            .Select(static x => TryGet(x.Labels, "notify_users"))
            .FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x));

        notifyUsers ??= TryGet(payload.CommonLabels, "notify_users");

        var users = SplitUsers(notifyUsers);
        return users.Count > 0
            ? users
            : options.DefaultToUsers
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Select(static x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    int ResolveAgentId(GrafanaWebhookPayload payload, GrafanaAlertOptions options)
    {
        var labelValue = payload.Alerts
            .Select(static x => FirstValue(x.Labels, AgentIdLabelKeys))
            .FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x));

        labelValue ??= FirstValue(payload.CommonLabels, AgentIdLabelKeys);
        if (string.IsNullOrWhiteSpace(labelValue))
        {
            return options.AgentId;
        }

        if (int.TryParse(labelValue.Trim(), out var agentId) && agentId > 0)
        {
            return agentId;
        }

        _logger.LogWarning(
            "Invalid Grafana alert AgentId label value {AgentIdLabelValue}. FallbackAgentId={FallbackAgentId}",
            labelValue,
            options.AgentId);
        return options.AgentId;
    }

    static IReadOnlyList<string> SplitUsers(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value
                .Split(UserSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    static string BuildContent(GrafanaWebhookPayload payload)
    {
        var builder = new StringBuilder();
        AppendLine(builder, "[Grafana告警] " + NullIfWhiteSpace(payload.Status));
        AppendLine(builder, "标题：" + NullIfWhiteSpace(payload.Title));
        AppendLine(builder, "消息：" + NullIfWhiteSpace(payload.Message));
        AppendLine(builder, "接收器：" + NullIfWhiteSpace(payload.Receiver));

        AppendDictionary(builder, "公共标签", payload.CommonLabels, skipControlKeys: true);
        AppendDictionary(builder, "公共注解", payload.CommonAnnotations, skipControlKeys: false);

        for (var i = 0; i < payload.Alerts.Count; i++)
        {
            var alert = payload.Alerts[i];
            builder.AppendLine();
            builder.AppendLine($"告警 {i + 1}/{payload.Alerts.Count}");
            AppendLine(builder, "状态：" + NullIfWhiteSpace(alert.Status));
            AppendLine(builder, "名称：" + FirstValue(alert.Labels, "alertname", "name"));
            AppendLine(builder, "级别：" + FirstValue(alert.Labels, "severity", "level"));
            AppendLine(builder, "实例：" + FirstValue(alert.Labels, "instance", "host", "pod"));
            AppendLine(builder, "任务：" + FirstValue(alert.Labels, "job", "service", "app"));
            AppendLine(builder, "摘要：" + FirstValue(alert.Annotations, "summary"));
            AppendLine(builder, "描述：" + FirstValue(alert.Annotations, "description", "message"));
            AppendLine(builder, "开始时间：" + FormatTime(alert.StartsAt));
            AppendLine(builder, "结束时间：" + FormatTime(alert.EndsAt));
        }

        return builder.ToString().Trim();
    }

    static void AppendDictionary(
        StringBuilder builder,
        string title,
        IReadOnlyDictionary<string, string>? values,
        bool skipControlKeys)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        var rows = values
            .Where(x => !skipControlKeys || !IsControlLabel(x.Key))
            .OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(title + "：");
        foreach (var (key, value) in rows)
        {
            builder.AppendLine($"{key}={value}");
        }
    }

    static bool IsControlLabel(string key)
    {
        return key.StartsWith("notify_", StringComparison.OrdinalIgnoreCase) ||
            AgentIdLabelKeys.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
    }

    static void AppendLine(StringBuilder builder, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !value.EndsWith('：') && !value.EndsWith(':'))
        {
            builder.AppendLine(value);
        }
    }

    static string? FirstValue(IReadOnlyDictionary<string, string>? values, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = TryGet(values, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    static string? TryGet(IReadOnlyDictionary<string, string>? values, string key)
    {
        if (values is null)
        {
            return null;
        }

        foreach (var item in values)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return item.Value;
            }
        }

        return null;
    }

    static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    static string? FormatTime(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}

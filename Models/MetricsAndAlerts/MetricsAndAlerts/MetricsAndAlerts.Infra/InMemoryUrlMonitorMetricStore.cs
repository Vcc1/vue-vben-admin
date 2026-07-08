using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using MetricsAndAlertsApp;

namespace Infrastructure.MetricsAndAlertsApp;

public sealed class InMemoryUrlMonitorMetricStore : IUrlMonitorMetricRecorder, IUrlMonitorMetricExporter
{
    readonly ConcurrentDictionary<string, UrlMonitorMetricSample> _samples = new();

    public void Record(TargetUrlMonitorRule rule, UrlMonitorCheckResult result, DateTime checkedAtUtc)
    {
        var sample = new UrlMonitorMetricSample
        {
            RuleId = rule.Id.ToString(CultureInfo.InvariantCulture),
            TargetKey = rule.TargetKey,
            TargetType = MetricsAndAlertsUtilities.GetPrometheusTargetTypeLabel(rule.TargetType),
            DurationSeconds = result.DurationSeconds,
            Success = result.IsSuccess ? 1 : 0,
            StatusCode = result.StatusCode ?? 0,
            LastCheckTimestampSeconds = MetricsAndAlertsUtilities.ToUnixTimeSeconds(checkedAtUtc)
        };

        _samples[$"{sample.RuleId}|{sample.TargetKey}|{sample.TargetType}"] = sample;
    }

    public string Export()
    {
        var builder = new StringBuilder();
        AppendHeader(builder, "target_url_monitor_duration_seconds", "URL monitor request duration in seconds.");
        foreach (var sample in Samples())
        {
            builder.Append("target_url_monitor_duration_seconds");
            AppendLabels(builder, sample);
            builder.Append(' ');
            builder.AppendLine(sample.DurationSeconds.ToString("0.######", CultureInfo.InvariantCulture));
        }

        AppendHeader(builder, "target_url_monitor_success", "URL monitor success result. 1 means success, 0 means failure.");
        foreach (var sample in Samples())
        {
            builder.Append("target_url_monitor_success");
            AppendLabels(builder, sample);
            builder.Append(' ');
            builder.AppendLine(sample.Success.ToString(CultureInfo.InvariantCulture));
        }

        AppendHeader(builder, "target_url_monitor_status_code", "URL monitor HTTP status code. 0 means no HTTP response.");
        foreach (var sample in Samples())
        {
            builder.Append("target_url_monitor_status_code");
            AppendLabels(builder, sample);
            builder.Append(' ');
            builder.AppendLine(sample.StatusCode.ToString(CultureInfo.InvariantCulture));
        }

        AppendHeader(builder, "target_url_monitor_last_check_timestamp_seconds", "URL monitor last check Unix timestamp seconds.");
        foreach (var sample in Samples())
        {
            builder.Append("target_url_monitor_last_check_timestamp_seconds");
            AppendLabels(builder, sample);
            builder.Append(' ');
            builder.AppendLine(sample.LastCheckTimestampSeconds.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    IReadOnlyList<UrlMonitorMetricSample> Samples()
    {
        return _samples.Values
            .OrderBy(x => x.RuleId, StringComparer.Ordinal)
            .ThenBy(x => x.TargetKey, StringComparer.Ordinal)
            .ToArray();
    }

    static void AppendHeader(StringBuilder builder, string name, string help)
    {
        builder.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        builder.Append("# TYPE ").Append(name).AppendLine(" gauge");
    }

    static void AppendLabels(StringBuilder builder, UrlMonitorMetricSample sample)
    {
        builder.Append("{rule_id=\"")
            .Append(EscapeLabel(sample.RuleId))
            .Append("\",target_key=\"")
            .Append(EscapeLabel(sample.TargetKey))
            .Append("\",target_type=\"")
            .Append(EscapeLabel(sample.TargetType))
            .Append("\"}");
    }

    static string EscapeLabel(string value)
    {
        return MetricsAndAlertsUtilities.EscapePrometheusLabelValue(value)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}

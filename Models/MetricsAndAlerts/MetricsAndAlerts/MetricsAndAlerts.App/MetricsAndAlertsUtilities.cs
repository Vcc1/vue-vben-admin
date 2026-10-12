using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MetricsAndAlertsApp;

public static class MetricsAndAlertsUtilities
{
    public static List<int> ParseNotifyMinuteQueue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<int> { 0 };
        }

        var items = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute) ? minute : -1)
            .Where(x => x >= 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return items.Count == 0 ? new List<int> { 0 } : items;
    }

    public static string FormatNotifyMinuteQueue(IEnumerable<int>? values)
    {
        var items = values?
            .Where(x => x >= 0)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        return items is { Length: > 0 }
            ? string.Join(',', items)
            : "0";
    }

    public static HashSet<int> ParseExpectedStatusCodes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<int> { 200 };
        }

        var items = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : -1)
            .Where(x => x > 0)
            .ToHashSet();

        return items.Count == 0 ? new HashSet<int> { 200 } : items;
    }

    public static string FormatExpectedStatusCodes(IEnumerable<int>? values)
    {
        var items = values?
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        return items is { Length: > 0 }
            ? string.Join(',', items)
            : "200";
    }

    public static List<string> ParseNotifyUsers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? FormatNotifyUsers(IEnumerable<string>? users)
    {
        var items = users?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return items is { Length: > 0 } ? string.Join(',', items) : null;
    }

    public static string GetDisplayName(string targetKey, string? targetName)
    {
        return string.IsNullOrWhiteSpace(targetName) ? targetKey : targetName.Trim();
    }

    public static string GetTargetTypeName(MonitorTargetType targetType)
    {
        return targetType switch
        {
            MonitorTargetType.Application => "应用",
            MonitorTargetType.Server => "服务器",
            _ => targetType.ToString()
        };
    }

    public static string GetPrometheusTargetTypeLabel(MonitorTargetType targetType)
    {
        return targetType switch
        {
            MonitorTargetType.Application => "application",
            MonitorTargetType.Server => "server",
            _ => targetType.ToString().ToLowerInvariant()
        };
    }

    public static string EscapePrometheusLabelValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    public static string BuildAlertKey(AlertType alertType, long ruleId, string? scopeKey = null)
    {
        return alertType switch
        {
            AlertType.TargetAliveCountLow => $"alive-count:{ruleId}",
            AlertType.TargetUrlMonitorFailed => $"url-monitor:{ruleId}",
            AlertType.MetricFormulaAlert => BuildMetricFormulaAlertKey(ruleId, scopeKey),
            _ => $"{(int)alertType}:{ruleId}"
        };
    }

    static string BuildMetricFormulaAlertKey(long ruleId, string? scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return $"metric-formula:{ruleId}";
        }

        var normalizedScopeKey = scopeKey.Trim();
        if (normalizedScopeKey.Length > 320)
        {
            var bytes = Encoding.UTF8.GetBytes(normalizedScopeKey);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            normalizedScopeKey = $"{normalizedScopeKey[..128]}#{hash}";
        }

        return $"metric-formula:{ruleId}:{normalizedScopeKey}";
    }

    public static long ToUnixTimeSeconds(DateTime utc)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }
}

using System.Globalization;
using System.Text.Json;
using MetricsAndAlertsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.HTTP.Prometheus;

public sealed class PrometheusQueryClient : IPrometheusQueryClient
{
    public const string HttpClientName = nameof(PrometheusQueryClient);

    readonly IHttpClientFactory _httpClientFactory;
    readonly IOptionsMonitor<PrometheusOptions> _options;
    readonly ILogger<PrometheusQueryClient> _logger;

    public PrometheusQueryClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PrometheusOptions> options,
        ILogger<PrometheusQueryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<int> GetOnlineCountAsync(
        string targetKey,
        MonitorTargetType targetType,
        int offlineSeconds,
        CancellationToken cancellationToken = default)
    {
        var result = targetType == MonitorTargetType.Application
            ? await QueryResultsAsync(
                BuildAppMetricsOnlineCountQuery(targetKey, offlineSeconds),
                cancellationToken)
            : await QueryResultsAsync(
                BuildOnlineCountQuery(targetKey, targetType, offlineSeconds),
                cancellationToken);
        if (result.GetArrayLength() == 0)
        {
            return 0;
        }

        var value = TryGetPrometheusValue(result[0], out var parsed) ? parsed : 0;
        return Math.Max(0, Convert.ToInt32(Math.Floor(value)));
    }

    public async Task<IReadOnlyList<TargetInstanceDto>> GetInstancesAsync(
        string targetKey,
        MonitorTargetType targetType,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var result = targetType == MonitorTargetType.Application
            ? await QueryResultsAsync(
                BuildAppMetricsInstancesQuery(targetKey),
                cancellationToken)
            : await QueryResultsAsync(
                BuildInstancesQuery(targetKey, targetType),
                cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var instances = new List<TargetInstanceDto>();

        foreach (var item in result.EnumerateArray())
        {
            if (!item.TryGetProperty("metric", out var metric))
            {
                continue;
            }

            var instanceKey = TryGetString(metric, "instance_key")
                ?? TryGetString(metric, "exported_instance");
            if (string.IsNullOrWhiteSpace(instanceKey) || !TryGetPrometheusValue(item, out var heartbeat))
            {
                continue;
            }

            var age = Math.Max(0, now - Convert.ToInt64(Math.Floor(heartbeat)));
            instances.Add(new TargetInstanceDto
            {
                InstanceKey = instanceKey,
                LastHeartbeatTimestamp = heartbeat,
                HeartbeatAgeSeconds = age,
                IsOnline = age <= Math.Max(1, options.DefaultOfflineSeconds)
            });
        }

        return instances.OrderBy(x => x.InstanceKey, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<PrometheusMetricSample>> QueryInstantAsync(
        string promql,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promql))
        {
            throw new ArgumentException("Promql is required.", nameof(promql));
        }

        var result = await QueryResultsAsync(promql.Trim(), cancellationToken);
        var samples = new List<PrometheusMetricSample>();

        foreach (var item in result.EnumerateArray())
        {
            if (!item.TryGetProperty("metric", out var metric) ||
                !TryGetPrometheusValue(item, out var value))
            {
                continue;
            }

            var labels = ReadLabels(metric);
            var instanceKey = TryGetString(metric, "exported_instance")
                ?? TryGetString(metric, "instance")
                ?? TryGetString(metric, "instance_key")
                ?? "_global";

            samples.Add(new PrometheusMetricSample
            {
                InstanceKey = instanceKey,
                Value = value,
                Labels = labels
            });
        }

        return samples;
    }

    async Task<JsonElement> QueryResultsAsync(
        string query,
        CancellationToken cancellationToken)
    {
        using var document = await QueryAsync(query, cancellationToken);
        return GetResults(document.RootElement).Clone();
    }

    async Task<JsonDocument> QueryAsync(string query, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var baseUrl = options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/query?query={Uri.EscapeDataString(query)}";
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));

        _logger.LogInformation("Querying Prometheus. Url={PrometheusUrl}, Query={PrometheusQuery}", url, query);
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Prometheus query failed. StatusCode={(int)response.StatusCode}, Body={body}");
        }

        var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var status = TryGetString(root, "status");
        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var error = TryGetString(root, "error");
            document.Dispose();
            throw new InvalidOperationException($"Prometheus query did not succeed. Error={error ?? body}");
        }

        return document;
    }

    JsonElement GetResults(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Prometheus response missing data.result array.");
        }

        return result;
    }

    string BuildOnlineCountQuery(string targetKey, MonitorTargetType targetType, int offlineSeconds)
    {
        var options = _options.CurrentValue;
        var metric = options.HeartbeatMetricName;
        var escapedTargetKey = MetricsAndAlertsUtilities.EscapePrometheusLabelValue(targetKey);
        var seconds = Math.Max(1, offlineSeconds);

        return "count((" +
            "time() - max by(instance_key) (" +
            $"{metric}{{target_key=\"{escapedTargetKey}\"}}" +
            $")) <= {seconds}) or vector(0)";
    }

    string BuildInstancesQuery(string targetKey, MonitorTargetType targetType)
    {
        var options = _options.CurrentValue;
        var metric = options.HeartbeatMetricName;
        var escapedTargetKey = MetricsAndAlertsUtilities.EscapePrometheusLabelValue(targetKey);
        return $"max by(instance_key) ({metric}{{target_key=\"{escapedTargetKey}\"}})";
    }

    string BuildAppMetricsOnlineCountQuery(string targetKey, int offlineSeconds)
    {
        var escapedTargetKey = MetricsAndAlertsUtilities.EscapePrometheusLabelValue(targetKey);
        var seconds = Math.Max(1, offlineSeconds);
        return "count((" +
            "time() - max by(exported_instance) (" +
            $"app_last_heartbeat_timestamp{{exported_job=\"{escapedTargetKey}\"}}" +
            $")) <= {seconds}) or vector(0)";
    }

    string BuildAppMetricsInstancesQuery(string targetKey)
    {
        var escapedTargetKey = MetricsAndAlertsUtilities.EscapePrometheusLabelValue(targetKey);
        return $"max by(exported_instance) (app_last_heartbeat_timestamp{{exported_job=\"{escapedTargetKey}\"}})";
    }

    static string? TryGetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    static IReadOnlyDictionary<string, string> ReadLabels(JsonElement metric)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in metric.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                labels[property.Name] = property.Value.GetString() ?? "";
            }
        }

        return labels;
    }

    static bool TryGetPrometheusValue(JsonElement resultItem, out double value)
    {
        value = 0;
        if (!resultItem.TryGetProperty("value", out var valueArray) ||
            valueArray.ValueKind != JsonValueKind.Array ||
            valueArray.GetArrayLength() < 2)
        {
            return false;
        }

        var raw = valueArray[1].ValueKind == JsonValueKind.String
            ? valueArray[1].GetString()
            : valueArray[1].GetRawText();

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

namespace MetricsAndAlertsApp;

public static class MetricValueModes
{
    public const string Instant = "instant";
    public const string Changed = "changed";
}

public static class MetricAggregateModes
{
    public const string Average = "avg";
    public const string Sum = "sum";
    public const string Max = "max";
    public const string Min = "min";
    public const string Count = "count";
}

public sealed class MetricQueryItem
{
    public string Name { get; set; } = "";
    public string Promql { get; set; } = "";
    public string ValueMode { get; set; } = MetricValueModes.Instant;
    public string AggregateMode { get; set; } = MetricAggregateModes.Average;
}

public sealed class PrometheusMetricSample
{
    public string InstanceKey { get; set; } = "_global";
    public double Value { get; set; }
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
}

public sealed class MetricAlertRule
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public string QueryItemsJson { get; set; } = "[]";
    public string Formula { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string NotifyMinuteQueue { get; set; } = "0,5,10,120";
    public string? NotifyUsers { get; set; }
    public string? MessageTemplate { get; set; }
    public string? RecoverTemplate { get; set; }
    public string? LastValuesJson { get; set; }
    public bool AggregateInstances { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ScheduledMetricMessageRule
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public string QueryItemsJson { get; set; } = "[]";
    public string MessageTemplate { get; set; } = "";
    public string Cron { get; set; } = "";
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? NotifyUsers { get; set; }
    public bool AggregateInstances { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ScheduledMessageSendLog
{
    public long Id { get; set; }
    public long RuleId { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UpsertMetricAlertRuleRequest
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string Formula { get; set; } = "";
    public int DurationMinutes { get; set; }
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
    public string? MessageTemplate { get; set; }
    public string? RecoverTemplate { get; set; }
    public bool AggregateInstances { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class MetricAlertRuleDto
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string Formula { get; set; } = "";
    public int DurationMinutes { get; set; }
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
    public string? MessageTemplate { get; set; }
    public string? RecoverTemplate { get; set; }
    public bool AggregateInstances { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class MetricAlertRuleListQuery
{
    public string? TargetKey { get; set; }
    public MonitorTargetType? TargetType { get; set; }
    public bool IncludeDisabled { get; set; }
}

public sealed class MetricAlertRulePreviewRequest
{
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string Formula { get; set; } = "";
    public string? MessageTemplate { get; set; }
    public string? RecoverTemplate { get; set; }
    public bool AggregateInstances { get; set; }
}

public sealed class MetricInstanceFormulaResult
{
    public string Instance { get; set; } = "_global";
    public bool IsAbnormal { get; set; }
    public Dictionary<string, double> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> PreviousValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ErrorMessage { get; set; }
}

public sealed class MetricAlertPreviewDto
{
    public bool IsAbnormal { get; set; }
    public string Formula { get; set; } = "";
    public List<MetricInstanceFormulaResult> Instances { get; set; } = new();
    public string CurrentValues { get; set; } = "";
    public string AbnormalInstances { get; set; } = "";
    public string MessageContent { get; set; } = "";
    public string RecoverContent { get; set; } = "";
    public bool AggregateInstances { get; set; }
}

public sealed class MetricAlertRuleTestSendRequest
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string Formula { get; set; } = "";
    public string? MessageTemplate { get; set; }
    public string? RecoverTemplate { get; set; }
    public List<string> NotifyUsers { get; set; } = new();
    public bool AggregateInstances { get; set; }
}

public sealed class UpsertScheduledMetricMessageRuleRequest
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string MessageTemplate { get; set; } = "";
    public string Cron { get; set; } = "";
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    public List<string> NotifyUsers { get; set; } = new();
    public bool AggregateInstances { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class ScheduledMetricMessageRuleDto
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string MessageTemplate { get; set; } = "";
    public string Cron { get; set; } = "";
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public List<string> NotifyUsers { get; set; } = new();
    public bool AggregateInstances { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ScheduledMetricMessageRuleListQuery
{
    public string? TargetKey { get; set; }
    public MonitorTargetType? TargetType { get; set; }
    public bool IncludeDisabled { get; set; }
}

public sealed class ScheduledMetricMessagePreviewRequest
{
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string MessageTemplate { get; set; } = "";
    public bool AggregateInstances { get; set; }
}

public sealed class ScheduledMetricMessagePreviewDto
{
    public string MessageContent { get; set; } = "";
    public string CurrentValues { get; set; } = "";
    public List<MetricInstanceQueryValues> Instances { get; set; } = new();
    public bool AggregateInstances { get; set; }
}

public sealed class ScheduledMetricMessageRuleTestSendRequest
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string RuleName { get; set; } = "";
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    public string MessageTemplate { get; set; } = "";
    public List<string> NotifyUsers { get; set; } = new();
    public bool AggregateInstances { get; set; }
}

public sealed class TestSendMessageDto
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class MetricInstanceQueryValues
{
    public string Instance { get; set; } = "_global";
    public Dictionary<string, double> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> PreviousValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MetricQueryRunResult
{
    public List<MetricInstanceQueryValues> Instances { get; set; } = new();
    public string LastValuesJson { get; set; } = "{}";
}

public sealed class MetricFormulaContext
{
    public IReadOnlyDictionary<string, double> Values { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> PreviousValues { get; init; } = new Dictionary<string, double>();
}

public sealed class MetricFormulaEvaluationResult
{
    public bool IsMatch { get; init; }
}

public sealed class MetricTemplateContext
{
    public IReadOnlyDictionary<string, object?> Model { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, string?> FlatValues { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

public interface IMetricRulesRepository
{
    Task<MetricAlertRule> UpsertMetricAlertRuleAsync(MetricAlertRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetricAlertRule>> ListMetricAlertRulesAsync(MetricAlertRuleListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MetricAlertRule>> ListEnabledMetricAlertRulesAsync(CancellationToken cancellationToken = default);
    Task<bool> DisableMetricAlertRuleAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateMetricAlertRuleLastValuesAsync(long id, string lastValuesJson, CancellationToken cancellationToken = default);

    Task<ScheduledMetricMessageRule> UpsertScheduledMetricMessageRuleAsync(ScheduledMetricMessageRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledMetricMessageRule>> ListScheduledMetricMessageRulesAsync(ScheduledMetricMessageRuleListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledMetricMessageRule>> ListDueScheduledMetricMessageRulesAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> DisableScheduledMetricMessageRuleAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateScheduledMetricMessageRuleRunAsync(long id, DateTime lastRunAtUtc, DateTime? nextRunAtUtc, CancellationToken cancellationToken = default);
    Task AddScheduledMessageSendLogAsync(ScheduledMessageSendLog log, CancellationToken cancellationToken = default);
}

public interface IMetricFormulaEvaluator
{
    MetricFormulaEvaluationResult Evaluate(string formula, MetricFormulaContext context);
    void Validate(string formula, IReadOnlyCollection<string> variableNames);
}

public interface IMetricQueryRunner
{
    Task<MetricQueryRunResult> RunAsync(
        IReadOnlyList<MetricQueryItem> queryItems,
        string? previousValuesJson,
        CancellationToken cancellationToken = default);
}

public interface ITemplateRenderer
{
    string Render(string template, MetricTemplateContext context);
}

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
    /// <summary>
    /// 规则 Id；为 0 时视为新增。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 规则名称；为空时默认回退为公式文本。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 公式中可引用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 告警判断公式，返回 true 代表异常。
    /// </summary>
    public string Formula { get; set; } = "";
    /// <summary>
    /// 异常持续多少分钟后才正式触发告警。
    /// </summary>
    public int DurationMinutes { get; set; }
    /// <summary>
    /// 告警触发后的通知分钟队列，例如 0,5,10,120。
    /// </summary>
    public List<int> NotifyMinuteQueue { get; set; } = new();
    /// <summary>
    /// 指定通知接收人；为空时回退到监控目标默认通知人。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 告警通知模板；为空时使用默认模板。
    /// </summary>
    public string? MessageTemplate { get; set; }
    /// <summary>
    /// 恢复通知模板；为空时使用默认模板。
    /// </summary>
    public string? RecoverTemplate { get; set; }
    /// <summary>
    /// 是否按聚合结果计算；为 false 时按实例逐个计算。
    /// </summary>
    public bool AggregateInstances { get; set; }
    /// <summary>
    /// 是否启用规则。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

public sealed class MetricAlertRuleDto
{
    /// <summary>
    /// 规则 Id。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 规则名称。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 规则使用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 告警判断公式。
    /// </summary>
    public string Formula { get; set; } = "";
    /// <summary>
    /// 异常持续多久后触发告警，单位分钟。
    /// </summary>
    public int DurationMinutes { get; set; }
    /// <summary>
    /// 告警后的通知分钟队列。
    /// </summary>
    public List<int> NotifyMinuteQueue { get; set; } = new();
    /// <summary>
    /// 规则级别通知接收人列表。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 告警通知模板。
    /// </summary>
    public string? MessageTemplate { get; set; }
    /// <summary>
    /// 恢复通知模板。
    /// </summary>
    public string? RecoverTemplate { get; set; }
    /// <summary>
    /// 是否按聚合结果计算。
    /// </summary>
    public bool AggregateInstances { get; set; }
    /// <summary>
    /// 是否启用规则。
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 最后更新时间（UTC）。
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

public sealed class MetricAlertRuleListQuery
{
    /// <summary>
    /// 按监控目标唯一键筛选。
    /// </summary>
    public string? TargetKey { get; set; }
    /// <summary>
    /// 按监控目标类型筛选。
    /// </summary>
    public MonitorTargetType? TargetType { get; set; }
    /// <summary>
    /// 是否包含已禁用规则。
    /// </summary>
    public bool IncludeDisabled { get; set; }
}

public sealed class MetricAlertRulePreviewRequest
{
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 预览时使用的规则名称。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 预览时使用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 预览时使用的告警判断公式。
    /// </summary>
    public string Formula { get; set; } = "";
    /// <summary>
    /// 预览告警模板；为空时使用默认模板。
    /// </summary>
    public string? MessageTemplate { get; set; }
    /// <summary>
    /// 预览恢复模板；为空时使用默认模板。
    /// </summary>
    public string? RecoverTemplate { get; set; }
    /// <summary>
    /// 是否按聚合结果计算。
    /// </summary>
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
    /// <summary>
    /// 规则 Id；仅用于测试日志记录。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 测试发送时使用的规则名称。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 测试发送时使用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 测试发送时使用的告警判断公式。
    /// </summary>
    public string Formula { get; set; } = "";
    /// <summary>
    /// 测试发送时使用的告警模板。
    /// </summary>
    public string? MessageTemplate { get; set; }
    /// <summary>
    /// 测试发送时使用的恢复模板。
    /// </summary>
    public string? RecoverTemplate { get; set; }
    /// <summary>
    /// 测试消息的接收人列表；为空时回退到监控目标默认通知人。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否按聚合结果计算。
    /// </summary>
    public bool AggregateInstances { get; set; }
}

public sealed class UpsertScheduledMetricMessageRuleRequest
{
    /// <summary>
    /// 规则 Id；为 0 时视为新增。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 规则名称；为空时默认回退为 Cron 表达式。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 消息模板中可引用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 定时报表消息模板；为空时使用默认模板。
    /// </summary>
    public string MessageTemplate { get; set; } = "";
    /// <summary>
    /// Cron 表达式，决定发送调度。
    /// </summary>
    public string Cron { get; set; } = "";
    /// <summary>
    /// Cron 解析使用的时区 Id。
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    /// <summary>
    /// 指定通知接收人；为空时回退到监控目标默认通知人。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否按聚合结果渲染；为 false 时按实例逐个渲染后合并。
    /// </summary>
    public bool AggregateInstances { get; set; }
    /// <summary>
    /// 是否启用规则。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

public sealed class ScheduledMetricMessageRuleDto
{
    /// <summary>
    /// 规则 Id。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 规则名称。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 规则使用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 定时报表消息模板。
    /// </summary>
    public string MessageTemplate { get; set; } = "";
    /// <summary>
    /// 发送调度的 Cron 表达式。
    /// </summary>
    public string Cron { get; set; } = "";
    /// <summary>
    /// Cron 解析使用的时区 Id。
    /// </summary>
    public string TimeZoneId { get; set; } = "Asia/Shanghai";
    /// <summary>
    /// 下次计划执行时间（UTC）。
    /// </summary>
    public DateTime? NextRunAt { get; set; }
    /// <summary>
    /// 最近一次执行时间（UTC）。
    /// </summary>
    public DateTime? LastRunAt { get; set; }
    /// <summary>
    /// 规则级别通知接收人列表。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否按聚合结果渲染消息。
    /// </summary>
    public bool AggregateInstances { get; set; }
    /// <summary>
    /// 是否启用规则。
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// 最后更新时间（UTC）。
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

public sealed class ScheduledMetricMessageRuleListQuery
{
    /// <summary>
    /// 按监控目标唯一键筛选。
    /// </summary>
    public string? TargetKey { get; set; }
    /// <summary>
    /// 按监控目标类型筛选。
    /// </summary>
    public MonitorTargetType? TargetType { get; set; }
    /// <summary>
    /// 是否包含已禁用规则。
    /// </summary>
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
    /// <summary>
    /// 规则 Id；仅用于测试日志记录。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 关联的监控目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 关联的监控目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 测试发送时使用的规则名称。
    /// </summary>
    public string RuleName { get; set; } = "";
    /// <summary>
    /// 测试发送时使用的指标查询项列表。
    /// </summary>
    public List<MetricQueryItem> QueryItems { get; set; } = new();
    /// <summary>
    /// 测试发送时使用的消息模板。
    /// </summary>
    public string MessageTemplate { get; set; } = "";
    /// <summary>
    /// 测试消息的接收人列表；为空时回退到监控目标默认通知人。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否按聚合结果渲染消息。
    /// </summary>
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

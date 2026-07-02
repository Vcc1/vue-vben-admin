using Microsoft.Extensions.Logging;

namespace MetricsAndAlertsApp;

/// <summary>
/// 监控目标类型。
/// </summary>
public enum MonitorTargetType
{
    /// <summary>
    /// 应用。
    /// </summary>
    Application = 1,
    /// <summary>
    /// 服务器。
    /// </summary>
    Server = 2
}

/// <summary>
/// 告警类型。
/// </summary>
public enum AlertType
{
    /// <summary>
    /// 在线实例数低于阈值。
    /// </summary>
    TargetAliveCountLow = 1,
    /// <summary>
    /// URL 巡检失败。
    /// </summary>
    TargetUrlMonitorFailed = 2,
    MetricFormulaAlert = 3
}

public enum NotifyType
{
    Alert = 1,
    Recover = 2
}

public sealed class MonitorTargetGroup
{
    public long Id { get; set; }
    public string GroupName { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class MonitorTarget
{
    public long Id { get; set; }
    public long? GroupId { get; set; }
    public string TargetKey { get; set; } = "";
    public string? TargetName { get; set; }
    public MonitorTargetType TargetType { get; set; }
    public int SortOrder { get; set; }
    public string? NotifyUsers { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TargetAliveAlertRule
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    public int MinAliveCount { get; set; } = 1;
    public int OfflineSeconds { get; set; } = 60;
    public int DurationMinutes { get; set; }
    public string NotifyMinuteQueue { get; set; } = "0,5,10,120";
    public string? NotifyUsers { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TargetUrlMonitorRule
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    public string? UrlName { get; set; }
    public string Url { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public int TimeoutSeconds { get; set; } = 10;
    public int CheckIntervalSeconds { get; set; } = 30;
    public string ExpectedStatusCodes { get; set; } = "200";
    public int DurationMinutes { get; set; }
    public string NotifyMinuteQueue { get; set; } = "0,5,10,120";
    public string? NotifyUsers { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastCheckAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AlertState
{
    public long Id { get; set; }
    public AlertType AlertType { get; set; }
    public long RuleId { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string AlertKey { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? FirstAbnormalAt { get; set; }
    public DateTime? FiringAt { get; set; }
    public DateTime? RecoveredAt { get; set; }
    public DateTime? LastNotifyAt { get; set; }
    public int NextNotifyIndex { get; set; }
    public string? CurrentValue { get; set; }
    public string? ThresholdValue { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AlertNotifyLog
{
    public long Id { get; set; }
    public long AlertStateId { get; set; }
    public AlertType AlertType { get; set; }
    public NotifyType NotifyType { get; set; }
    public int? NotifyMinute { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PrometheusOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:9090";
    public int TimeoutSeconds { get; set; } = 10;
    public string HeartbeatMetricName { get; set; } = "monitor_target_heartbeat_timestamp_seconds";
    public int DefaultOfflineSeconds { get; set; } = 60;
}

public sealed class AlertOptions
{
    public string DefaultNotifyMinuteQueue { get; set; } = "0,5,10,120";
    public int TargetAliveWorkerIntervalSeconds { get; set; } = 30;
    public int UrlMonitorWorkerScanIntervalSeconds { get; set; } = 5;
    public int MetricAlertWorkerIntervalSeconds { get; set; } = 30;
    public int ScheduledMessageWorkerScanIntervalSeconds { get; set; } = 15;
}

public sealed class AlertNotificationOptions
{
    public List<string> DefaultToUsers { get; set; } = new();
    public int AgentId { get; set; } = 1000026;
}

/// <summary>
/// 新增或更新监控分组请求。
/// </summary>
public sealed class UpsertMonitorTargetGroupRequest
{
    /// <summary>
    /// 分组 Id；为 0 时视为新增。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 分组名称。
    /// </summary>
    public string GroupName { get; set; } = "";
    /// <summary>
    /// 排序值，越小越靠前。
    /// </summary>
    public int SortOrder { get; set; }
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 监控分组返回对象。
/// </summary>
public sealed class MonitorTargetGroupDto
{
    /// <summary>
    /// 分组 Id。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 分组名称。
    /// </summary>
    public string GroupName { get; set; } = "";
    /// <summary>
    /// 排序值，越小越靠前。
    /// </summary>
    public int SortOrder { get; set; }
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 新增或更新监控目标请求。
/// </summary>
public sealed class UpsertMonitorTargetRequest
{
    /// <summary>
    /// 监控分组 Id。
    /// </summary>
    public long? GroupId { get; set; }
    /// <summary>
    /// 目标唯一键，例如应用名或服务器标识。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 目标显示名称。
    /// </summary>
    public string? TargetName { get; set; }
    /// <summary>
    /// 目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    /// <summary>
    /// 排序值，越小越靠前。
    /// </summary>
    public int SortOrder { get; set; }
    /// <summary>
    /// 告警通知用户列表。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 最小在线实例数阈值。
    /// </summary>
    public int? MinAliveCount { get; set; }
    /// <summary>
    /// 心跳离线阈值，单位秒。
    /// </summary>
    public int? OfflineSeconds { get; set; }
    /// <summary>
    /// 持续满足告警条件多久后触发，单位分钟。
    /// </summary>
    public int? DurationMinutes { get; set; }
    /// <summary>
    /// 告警通知分钟队列，例如 0,5,10,120。
    /// </summary>
    public List<int> NotifyMinuteQueue { get; set; } = new();
    /// <summary>
    /// 巡检 URL。
    /// </summary>
    public string? CheckUrl { get; set; }
    /// <summary>
    /// 巡检 URL 名称。
    /// </summary>
    public string? CheckUrlName { get; set; }
    /// <summary>
    /// 巡检 HTTP 方法。
    /// </summary>
    public string HttpMethod { get; set; } = "GET";
    /// <summary>
    /// URL 巡检超时时间，单位秒。
    /// </summary>
    public int? CheckTimeoutSeconds { get; set; }
    /// <summary>
    /// URL 巡检间隔，单位秒。
    /// </summary>
    public int? CheckIntervalSeconds { get; set; }
    /// <summary>
    /// 期望 HTTP 状态码列表。
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; } = new();
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

public sealed class MonitorTargetListQuery
{
    public MonitorTargetType? TargetType { get; set; }
    public string? Keyword { get; set; }
    public long? GroupId { get; set; }
    public string? TargetKeyEquals { get; set; }
}

/// <summary>
/// 监控目标列表项。
/// </summary>
public sealed class MonitorTargetListItemDto
{
    /// <summary>
    /// 目标 Id。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 所属分组 Id。
    /// </summary>
    public long? GroupId { get; set; }
    /// <summary>
    /// 所属分组名称。
    /// </summary>
    public string? GroupName { get; set; }
    /// <summary>
    /// 目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 目标显示名称。
    /// </summary>
    public string? TargetName { get; set; }
    /// <summary>
    /// 目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    /// <summary>
    /// 目标类型文本。
    /// </summary>
    public string TargetTypeName { get; set; } = "";
    /// <summary>
    /// 排序值。
    /// </summary>
    public int SortOrder { get; set; }
    /// <summary>
    /// 通知用户列表。
    /// </summary>
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; }
    /// <summary>
    /// 最小在线实例数阈值。
    /// </summary>
    public int? MinAliveCount { get; set; }
    /// <summary>
    /// 心跳离线阈值，单位秒。
    /// </summary>
    public int? OfflineSeconds { get; set; }
    /// <summary>
    /// 持续满足告警条件多久后触发，单位分钟。
    /// </summary>
    public int? DurationMinutes { get; set; }
    /// <summary>
    /// 告警通知分钟队列。
    /// </summary>
    public List<int> NotifyMinuteQueue { get; set; } = new();
    /// <summary>
    /// 当前在线实例数。
    /// </summary>
    public int? OnlineCount { get; set; }
    /// <summary>
    /// 查询在线实例失败时的错误信息。
    /// </summary>
    public string? QueryError { get; set; }
    /// <summary>
    /// 在线实例数告警规则是否启用。
    /// </summary>
    public bool AliveAlertEnabled { get; set; }
    /// <summary>
    /// 当前是否处于在线实例数告警中。
    /// </summary>
    public bool AliveAlerting { get; set; }
    /// <summary>
    /// URL 巡检规则 Id。
    /// </summary>
    public long? CheckUrlRuleId { get; set; }
    /// <summary>
    /// URL 巡检名称。
    /// </summary>
    public string? CheckUrlName { get; set; }
    /// <summary>
    /// URL 巡检地址。
    /// </summary>
    public string? CheckUrl { get; set; }
    /// <summary>
    /// URL 巡检 HTTP 方法。
    /// </summary>
    public string? HttpMethod { get; set; }
    /// <summary>
    /// URL 巡检超时时间，单位秒。
    /// </summary>
    public int? CheckTimeoutSeconds { get; set; }
    /// <summary>
    /// URL 巡检间隔，单位秒。
    /// </summary>
    public int? CheckIntervalSeconds { get; set; }
    /// <summary>
    /// 期望 HTTP 状态码列表。
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; } = new();
    /// <summary>
    /// URL 巡检规则数量。
    /// </summary>
    public int UrlRuleCount { get; set; }
    /// <summary>
    /// 当前处于告警中的 URL 巡检规则数量。
    /// </summary>
    public int UrlAlertingCount { get; set; }
}

/// <summary>
/// 目标实例信息。
/// </summary>
public sealed class TargetInstanceDto
{
    /// <summary>
    /// 实例标识。
    /// </summary>
    public string InstanceKey { get; set; } = "";
    /// <summary>
    /// 最近一次心跳时间戳，Unix 秒。
    /// </summary>
    public double LastHeartbeatTimestamp { get; set; }
    /// <summary>
    /// 当前距离最近一次心跳的秒数。
    /// </summary>
    public long HeartbeatAgeSeconds { get; set; }
    /// <summary>
    /// 当前是否在线。
    /// </summary>
    public bool IsOnline { get; set; }
}

/// <summary>
/// 新增或更新在线实例数告警规则请求。
/// </summary>
public sealed class UpsertTargetAliveAlertRuleRequest
{
    public long Id { get; set; }
    /// <summary>
    /// 目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    /// <summary>
    /// 最小在线实例数阈值。
    /// </summary>
    public int MinAliveCount { get; set; } = 1;
    /// <summary>
    /// 离线判定秒数。
    /// </summary>
    public int OfflineSeconds { get; set; } = 60;
    /// <summary>
    /// 告警持续分钟数阈值。
    /// </summary>
    public int DurationMinutes { get; set; }
    /// <summary>
    /// 通知分钟队列。
    /// </summary>
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

public sealed class TargetAliveAlertRuleDto
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    public int MinAliveCount { get; set; }
    public int OfflineSeconds { get; set; }
    public int DurationMinutes { get; set; }
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 新增或更新 URL 巡检规则请求。
/// </summary>
public sealed class UpsertTargetUrlMonitorRuleRequest
{
    /// <summary>
    /// 规则 Id；为 0 时视为新增。
    /// </summary>
    public long Id { get; set; }
    /// <summary>
    /// 目标唯一键。
    /// </summary>
    public string TargetKey { get; set; } = "";
    /// <summary>
    /// 目标类型。
    /// </summary>
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    /// <summary>
    /// URL 名称。
    /// </summary>
    public string? UrlName { get; set; }
    /// <summary>
    /// 待巡检 URL。
    /// </summary>
    public string Url { get; set; } = "";
    /// <summary>
    /// HTTP 方法。
    /// </summary>
    public string HttpMethod { get; set; } = "GET";
    /// <summary>
    /// 超时时间，单位秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
    /// <summary>
    /// 巡检间隔，单位秒。
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;
    /// <summary>
    /// 期望 HTTP 状态码列表。
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; } = new();
    /// <summary>
    /// 告警持续分钟数阈值。
    /// </summary>
    public int DurationMinutes { get; set; }
    /// <summary>
    /// 通知分钟队列。
    /// </summary>
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

public sealed class TargetUrlMonitorRuleDto
{
    public long Id { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string? RuleName { get; set; }
    public string? UrlName { get; set; }
    public string Url { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public int TimeoutSeconds { get; set; }
    public int CheckIntervalSeconds { get; set; }
    public List<int> ExpectedStatusCodes { get; set; } = new();
    public int DurationMinutes { get; set; }
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AlertStateListQuery
{
    public string? TargetKey { get; set; }
    public MonitorTargetType? TargetType { get; set; }
    public AlertType? AlertType { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AlertStateDto
{
    public long Id { get; set; }
    public AlertType AlertType { get; set; }
    public long RuleId { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string AlertKey { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? FirstAbnormalAt { get; set; }
    public DateTime? FiringAt { get; set; }
    public DateTime? RecoveredAt { get; set; }
    public DateTime? LastNotifyAt { get; set; }
    public int NextNotifyIndex { get; set; }
    public string? CurrentValue { get; set; }
    public string? ThresholdValue { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AlertNotifyLogListQuery
{
    public string? TargetKey { get; set; }
    public MonitorTargetType? TargetType { get; set; }
    public AlertType? AlertType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public sealed class AlertNotifyLogDto
{
    public long Id { get; set; }
    public long AlertStateId { get; set; }
    public AlertType AlertType { get; set; }
    public NotifyType NotifyType { get; set; }
    public int? NotifyMinute { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AlertCheckResult
{
    public AlertType AlertType { get; set; }
    public long RuleId { get; set; }
    public string? AlertKey { get; set; }
    public string TargetKey { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public bool IsNormal { get; set; }
    public int DurationMinutes { get; set; }
    public List<int> NotifyMinuteQueue { get; set; } = new();
    public string? CurrentValue { get; set; }
    public string? ThresholdValue { get; set; }
    public string? ErrorMessage { get; set; }
    public int? OfflineSeconds { get; set; }
    public string? UrlName { get; set; }
    public string? Url { get; set; }
    public string? RuleName { get; set; }
    public string? Formula { get; set; }
    public string? Instance { get; set; }
    public string? CurrentValues { get; set; }
    public string? AbnormalInstances { get; set; }
    public string? MessageTemplate { get; set; }
    public string? RecoverTemplate { get; set; }
    public bool AggregateInstances { get; set; }
    public MetricInstanceQueryValues? MetricCurrentInstance { get; set; }
    public List<MetricInstanceQueryValues> MetricInstances { get; set; } = new();
    public List<MetricInstanceFormulaResult> MetricAbnormalInstances { get; set; } = new();
    public List<TargetInstanceDto> RecentOfflineInstances { get; set; } = new();
    public List<string> NotifyUsers { get; set; } = new();
}

public sealed class AlertMessage
{
    public AlertType AlertType { get; set; }
    public NotifyType NotifyType { get; set; }
    public IReadOnlyList<string> ToUsers { get; set; } = Array.Empty<string>();
    public string TargetKey { get; set; } = "";
    public string TargetName { get; set; } = "";
    public MonitorTargetType TargetType { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? CurrentValue { get; set; }
    public string? ThresholdValue { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class NotifyResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    public static NotifyResult Success() => new() { IsSuccess = true };
    public static NotifyResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}

public sealed class UrlMonitorCheckResult
{
    public bool IsSuccess { get; init; }
    public int? StatusCode { get; init; }
    public double DurationSeconds { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class UrlMonitorMetricSample
{
    public required string RuleId { get; init; }
    public required string TargetKey { get; init; }
    public required string TargetType { get; init; }
    public double DurationSeconds { get; init; }
    public int Success { get; init; }
    public int StatusCode { get; init; }
    public long LastCheckTimestampSeconds { get; init; }
}

public interface IMetricsAndAlertsRepository
{
    Task<MonitorTargetGroup> UpsertGroupAsync(MonitorTargetGroup group, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonitorTargetGroup>> ListGroupsAsync(bool includeDisabled = false, CancellationToken cancellationToken = default);
    Task<bool> DisableGroupAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> GroupExistsAsync(long id, CancellationToken cancellationToken = default);

    Task<MonitorTarget> UpsertMonitorTargetAsync(MonitorTarget target, CancellationToken cancellationToken = default);
    Task<MonitorTarget?> GetMonitorTargetAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonitorTargetListItemDto>> ListMonitorTargetsAsync(MonitorTargetListQuery query, CancellationToken cancellationToken = default);

    Task<TargetAliveAlertRule> UpsertTargetAliveAlertRuleAsync(TargetAliveAlertRule rule, CancellationToken cancellationToken = default);
    Task<TargetAliveAlertRule?> GetTargetAliveAlertRuleAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TargetAliveAlertRule>> ListTargetAliveAlertRulesAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TargetAliveAlertRule>> ListEnabledTargetAliveAlertRulesAsync(CancellationToken cancellationToken = default);
    Task<bool> DisableTargetAliveAlertRuleAsync(long id, CancellationToken cancellationToken = default);

    Task<TargetUrlMonitorRule> UpsertTargetUrlMonitorRuleAsync(TargetUrlMonitorRule rule, CancellationToken cancellationToken = default);
    Task<TargetUrlMonitorRule?> GetFirstTargetUrlMonitorRuleAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TargetUrlMonitorRule>> ListTargetUrlMonitorRulesAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TargetUrlMonitorRule>> ListDueTargetUrlMonitorRulesAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> DisableTargetUrlMonitorRuleAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateTargetUrlMonitorLastCheckAsync(long id, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<AlertState?> GetAlertStateByKeyAsync(string alertKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertState>> ListAlertStatesByRuleAsync(AlertType alertType, long ruleId, CancellationToken cancellationToken = default);
    Task<AlertState> SaveAlertStateAsync(AlertState state, CancellationToken cancellationToken = default);
    Task DeleteAlertStatesAsync(IReadOnlyCollection<long> stateIds, CancellationToken cancellationToken = default);
    Task AddAlertNotifyLogAsync(AlertNotifyLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertStateDto>> ListAlertStatesAsync(AlertStateListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertNotifyLogDto>> ListAlertNotifyLogsAsync(AlertNotifyLogListQuery query, CancellationToken cancellationToken = default);
}

public interface IPrometheusQueryClient
{
    Task<int> GetOnlineCountAsync(string targetKey, MonitorTargetType targetType, int offlineSeconds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TargetInstanceDto>> GetInstancesAsync(string targetKey, MonitorTargetType targetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrometheusMetricSample>> QueryInstantAsync(string promql, CancellationToken cancellationToken = default);
}

public interface IAlertNotifier
{
    Task<NotifyResult> SendAlertAsync(AlertMessage message, CancellationToken cancellationToken = default);
    Task<NotifyResult> SendRecoverAsync(AlertMessage message, CancellationToken cancellationToken = default);
    Task<NotifyResult> SendMessageAsync(AlertMessage message, CancellationToken cancellationToken = default);
}

public interface IUrlMonitorClient
{
    Task<UrlMonitorCheckResult> CheckAsync(TargetUrlMonitorRule rule, CancellationToken cancellationToken = default);
}

public interface IUrlMonitorMetricRecorder
{
    void Record(TargetUrlMonitorRule rule, UrlMonitorCheckResult result, DateTime checkedAtUtc);
}

public interface IUrlMonitorMetricExporter
{
    string Export();
}

public static partial class LoggerMessages
{
    [LoggerMessage(EventId = 30001, Level = LogLevel.Error, Message = "Target alive alert worker failed.")]
    public static partial void TargetAliveWorkerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 30002, Level = LogLevel.Error, Message = "URL monitor worker failed.")]
    public static partial void UrlMonitorWorkerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 30003, Level = LogLevel.Error, Message = "Metric alert worker failed.")]
    public static partial void MetricAlertWorkerFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 30004, Level = LogLevel.Error, Message = "Scheduled metric message worker failed.")]
    public static partial void ScheduledMetricMessageWorkerFailed(this ILogger logger, Exception exception);
}

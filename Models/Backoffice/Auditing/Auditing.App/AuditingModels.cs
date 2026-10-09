namespace Auditing.App;

/// <summary>
/// 一条安全事件（如 refresh token 重放攻击）。actor/IP/UA 等由记录器从当前请求上下文补全。
/// </summary>
public sealed record SecurityEvent(
    string EventType,
    string Severity,
    string? Summary = null,
    object? Detail = null,
    string? ActorId = null,
    string? ActorName = null);

public interface ISecurityEventLogger
{
    Task LogAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}

/// <summary>
/// 一次被审计请求的快照。actor 信息与发生时间由存储层补全。
/// FeatureName / OperationName 是给运营看的中文功能名与操作名，由控制器上的
/// <see cref="RequestAuditAttribute.Feature"/> 与 action 上的 <see cref="AuditOperationAttribute"/> 声明。
/// </summary>
public sealed record RequestAuditEntry(
    string HttpMethod,
    string Path,
    string? QueryString,
    string? ControllerName,
    string? ActionName,
    string? FeatureName,
    string? OperationName,
    int StatusCode,
    long DurationMs,
    string? RequestBody,
    bool IsRequestBodyTruncated,
    string? ResponseBody,
    bool IsResponseBodyTruncated);

/// <summary>
/// 声明某个 action 在操作审计里显示的操作名（如"强制下线"）。
/// 刻意不做成 filter：[RequestAudit] 在控制器级已经注册了过滤器，
/// action 上再挂一个同类过滤器会导致同一请求写两条审计。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AuditOperationAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public interface IRequestAuditStore
{
    Task WriteAsync(RequestAuditEntry entry, CancellationToken cancellationToken);
}

/// <summary>
/// 审计查询过滤条件。所有字段均为可选，按非空逐项追加 where。
/// </summary>
public sealed record AuditQueryFilter(
    string? ActorName = null,
    string? Path = null,
    string? HttpMethod = null,
    string? EventType = null,
    string? Severity = null,
    string? Feature = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Take = 100);

public sealed record RequestAuditLogItem(
    long Id,
    string ActorType,
    string ActorId,
    string ActorName,
    string AuthenticationType,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    string HttpMethod,
    string Path,
    string? QueryString,
    string? ControllerName,
    string? ActionName,
    string? FeatureName,
    string? OperationName,
    int StatusCode,
    long DurationMs,
    string? RequestBody,
    bool IsRequestBodyTruncated,
    string? ResponseBody,
    bool IsResponseBodyTruncated,
    DateTime OccurredAtUtc);

public sealed record SecurityEventItem(
    long Id,
    string EventType,
    string Severity,
    string? Summary,
    string ActorType,
    string ActorId,
    string ActorName,
    string AuthenticationType,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    string? DetailJson,
    DateTime OccurredAtUtc);

public interface IAuditQueryService
{
    Task<IReadOnlyList<RequestAuditLogItem>> QueryRequestLogsAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<SecurityEventItem>> QuerySecurityEventsAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken);
}

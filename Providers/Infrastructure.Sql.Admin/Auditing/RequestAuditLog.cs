namespace Infrastructure.Sql.Admin.Auditing;

public sealed class RequestAuditLog
{
    public long Id { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string AuthenticationType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string? ControllerName { get; set; }
    public string? ActionName { get; set; }

    /// <summary>功能名称（中文，运营可读），由 [RequestAudit(Feature = "...")] 声明。</summary>
    public string? FeatureName { get; set; }

    /// <summary>操作名称（中文，运营可读），由 [AuditOperation("...")] 声明。</summary>
    public string? OperationName { get; set; }

    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? RequestBody { get; set; }
    public bool IsRequestBodyTruncated { get; set; }
    public string? ResponseBody { get; set; }
    public bool IsResponseBodyTruncated { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

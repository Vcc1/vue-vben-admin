namespace ApiGateway.Domain;

/// <summary>
/// Level 1 — 指向一个上游站点/服务 (如 http://hotel-service:5000)
/// </summary>
public sealed class GatewayApi
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>URL 路由键，如 "hotel-service"</summary>
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    /// <summary>API 级自定义 Header，JSON 格式 {"X-Tenant":"A"}</summary>
    public string? HeadersJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

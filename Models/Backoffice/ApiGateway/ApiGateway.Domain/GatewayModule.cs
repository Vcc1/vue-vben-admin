namespace ApiGateway.Domain;

/// <summary>
/// Level 2 — 同一站点内的功能模块 (如 "酒店匹配"、"价格查询")
/// </summary>
public sealed class GatewayModule
{
    public Guid Id { get; set; }
    public Guid ApiId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>路径前缀，如 "/api/matching"</summary>
    public string PathPrefix { get; set; } = string.Empty;
    /// <summary>模块级自定义 Header，JSON 格式，覆盖 API 同名 Header</summary>
    public string? HeadersJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

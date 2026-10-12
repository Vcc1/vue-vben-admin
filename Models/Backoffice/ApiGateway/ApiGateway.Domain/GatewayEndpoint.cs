namespace ApiGateway.Domain;

/// <summary>
/// Level 3 — 模块下的具体接口 (如 GET /hotels/{hotelId})
/// </summary>
public sealed class GatewayEndpoint
{
    public Guid Id { get; set; }
    public Guid ModuleId { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    /// <summary>端点路径，支持 {param} 参数，如 "/hotels/{hotelId}"</summary>
    public string Path { get; set; } = string.Empty;
    public string? OperationId { get; set; }
    public string? Summary { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

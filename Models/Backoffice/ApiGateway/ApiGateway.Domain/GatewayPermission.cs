namespace ApiGateway.Domain;

public sealed class GatewayPermission
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    /// <summary>必填 — 权限所属模块</summary>
    public Guid ModuleId { get; set; }
    /// <summary>可选 — null 表示该模块下所有端点都有权限</summary>
    public Guid? EndpointId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

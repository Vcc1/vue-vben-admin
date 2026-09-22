namespace Infrastructure.Sql.Admin.Identity;

public sealed class AdminPermission
{
    // Code 即主键，自然键、跨环境稳定，便于线下数据直接导入线上
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AdminRolePermission
{
    // 复合主键 (RoleName, PermissionCode)，全部用自然键，不用 Guid，避免跨环境导入冲突
    public string RoleName { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;

}

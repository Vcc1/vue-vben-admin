namespace Infrastructure.Sql.Admin.Identity;

/// <summary>
/// (用户, 设备) 的信任关系。受信设备登录仅需密码；新设备与被吊销设备登录需额外强认证。
/// deviceId 可被伪造或清除，本表只用于提高登录成本的 UX 安全层，不是安全边界。
/// </summary>
public sealed class AdminTrustedDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceInfoJson { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? LastLoginSid { get; set; }

    /// <summary>trusted / revoked</summary>
    public string Status { get; set; } = "trusted";

    public string? RevokedReason { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByActorId { get; set; }

    public AdminUser User { get; set; } = null!;
}

namespace Infrastructure.Sql.Admin.Identity;

/// <summary>
/// 一次成功登录派生的凭证（sid）。同一浏览器内主系统与各 OIDC 子系统共用一个 sid。
/// </summary>
public sealed class AdminSession
{
    public string Sid { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceInfoJson { get; set; }

    /// <summary>pwd（密码登录）/ otp（新设备或被踢设备，密码 + 强认证）/ sso（静默授权，未输入密码）</summary>
    public string LoginMethod { get; set; } = "pwd";

    /// <summary>L2 依据：最近一次密码核验时间，掉窗后需重新核验。</summary>
    public DateTime? PasswordVerifiedAtUtc { get; set; }

    /// <summary>L3 依据：最近一次强认证时间。</summary>
    public DateTime? StrongAuthAtUtc { get; set; }
    public string? StrongAuthMethod { get; set; }

    /// <summary>该 sid 已签发过 token 的 OIDC client_id 列表（JSON 数组），决定 backchannel 推送目标。</summary>
    public string? ClientIdsJson { get; set; }

    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? RevokedByActorId { get; set; }
    public string? RevokedByActorName { get; set; }
}

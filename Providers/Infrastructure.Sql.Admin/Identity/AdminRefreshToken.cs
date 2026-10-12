namespace Infrastructure.Sql.Admin.Identity;

public sealed class AdminRefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }

    /// <summary>所属登录凭证。刷新时据此判定该凭证是否已被吊销。</summary>
    public string? Sid { get; set; }
    public string? DeviceId { get; set; }
    public string? UserAgent { get; set; }

    public AdminUser User { get; set; } = null!;
}

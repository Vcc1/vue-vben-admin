namespace Infrastructure.Sql.Admin.Identity;

public sealed class AdminApiKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }

    public AdminUser User { get; set; } = null!;
}

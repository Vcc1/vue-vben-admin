namespace AdminIdentity.Infra;

public sealed class AdminIdentityOptions
{
    public const string SectionName = "AdminIdentity";

    public JwtOptions Jwt { get; set; } = new();
    public BootstrapOptions Bootstrap { get; set; } = new();
    public string DataProtectionKeyDirectory { get; set; } = Path.Combine("data", "admin-keys");

    public sealed class JwtOptions
    {
        public string Issuer { get; set; } = "Admin.Host";
        public string Audience { get; set; } = "Admin.Host.Api";
        public string? SigningKey { get; set; }
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 14;
    }

    public sealed class BootstrapOptions
    {
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Password { get; set; }
    }
}

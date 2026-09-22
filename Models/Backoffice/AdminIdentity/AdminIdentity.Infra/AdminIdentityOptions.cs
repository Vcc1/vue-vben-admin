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

        /// <summary>
        /// 已吊销 refresh token 被重放时的宽限窗口（秒）。窗口内视为并发竞态，仅拒绝；
        /// 超过窗口判定为令牌泄露，吊销整条令牌链并记录安全事件。
        /// </summary>
        public int RefreshTokenReuseLeewaySeconds { get; set; } = 15;
    }

    public sealed class BootstrapOptions
    {
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? Password { get; set; }
    }
}

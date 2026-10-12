namespace OidcServer.Infra;

public sealed class OidcServerOptions
{
    public const string SectionName = "OidcServer";

    public string Issuer { get; set; } = "https://admin.mengtu.com";
    public string AdminLoginUrl { get; set; } = "http://localhost:6001/auth/login";
    public TokenLifetimeOptions TokenLifetime { get; set; } = new();
    public SigningKeyOptions SigningKey { get; set; } = new();
    public EncryptionKeyOptions EncryptionKey { get; set; } = new();

    public sealed class TokenLifetimeOptions
    {
        public int AccessTokenMinutes { get; set; } = 60;
        public int RefreshTokenDays { get; set; } = 30;
        public int AuthorizationCodeMinutes { get; set; } = 10;
    }

    public sealed class SigningKeyOptions
    {
        public string Type { get; set; } = "symmetric";
        public string? Key { get; set; }
        public string? PfxPath { get; set; }
        public string? PfxPassword { get; set; }
        public string? PfxBase64 { get; set; }
    }

    public sealed class EncryptionKeyOptions
    {
        public string? Key { get; set; }
        public string? PfxPath { get; set; }
        public string? PfxPassword { get; set; }
        public string? PfxBase64 { get; set; }
    }
}

using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace OidcServer.Infra;

/// <summary>
/// 解析授权服务器签名密钥。除了 OpenIddict 自身签发 token，
/// Back-Channel Logout 的 logout_token 也必须用同一把密钥签名，客户端才能用 JWKS 验签。
/// </summary>
public static class OidcSigningKeyResolver
{
    public static X509Certificate2? ResolveCertificate(OidcServerOptions.SigningKeyOptions keyOptions)
    {
        ArgumentNullException.ThrowIfNull(keyOptions);

        if (!string.IsNullOrWhiteSpace(keyOptions.PfxBase64))
        {
            return X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(keyOptions.PfxBase64), keyOptions.PfxPassword);
        }

        if (string.Equals(keyOptions.Type, "rsa-pfx", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(keyOptions.PfxPath)
            && File.Exists(keyOptions.PfxPath))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(keyOptions.PfxPath, keyOptions.PfxPassword);
        }

        return null;
    }

    /// <summary>没有配置持久化密钥时返回 null（开发环境用 OpenIddict 的临时密钥，logout_token 无法离线验签）。</summary>
    public static SigningCredentials? ResolveCredentials(OidcServerOptions.SigningKeyOptions keyOptions)
    {
        var certificate = ResolveCertificate(keyOptions);
        return certificate is null
            ? null
            : new SigningCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaSha256);
    }
}

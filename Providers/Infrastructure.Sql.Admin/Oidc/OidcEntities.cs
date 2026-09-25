using OpenIddict.EntityFrameworkCore.Models;

namespace Infrastructure.Sql.Admin.Oidc;

public sealed class OidcApplication : OpenIddictEntityFrameworkCoreApplication<Guid, OidcAuthorization, OidcToken>
{
    public OidcApplication()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>OIDC Back-Channel Logout 1.0 的接收端点；为空表示该客户端不接受推送（退化为 access token 自然过期兜底）。</summary>
    public string? BackChannelLogoutUri { get; set; }

    /// <summary>客户端级最低会话等级闸门（1/2/3），authorize 时校验，不足则在登录页内 step-up。</summary>
    public int RequiredSessionLevel { get; set; } = 1;
}

public sealed class OidcScope : OpenIddictEntityFrameworkCoreScope<Guid>
{
    public OidcScope()
    {
        Id = Guid.CreateVersion7();
    }
}

public sealed class OidcAuthorization : OpenIddictEntityFrameworkCoreAuthorization<Guid, OidcApplication, OidcToken>
{
    public OidcAuthorization()
    {
        Id = Guid.CreateVersion7();
    }
}

public sealed class OidcToken : OpenIddictEntityFrameworkCoreToken<Guid, OidcApplication, OidcAuthorization>
{
    public OidcToken()
    {
        Id = Guid.CreateVersion7();
    }
}

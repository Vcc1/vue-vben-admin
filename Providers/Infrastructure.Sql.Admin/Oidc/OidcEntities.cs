using OpenIddict.EntityFrameworkCore.Models;

namespace Infrastructure.Sql.Admin.Oidc;

public sealed class OidcApplication : OpenIddictEntityFrameworkCoreApplication<Guid, OidcAuthorization, OidcToken>
{
    public OidcApplication()
    {
        Id = Guid.CreateVersion7();
    }
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

using Microsoft.Extensions.DependencyInjection;

namespace OidcServer.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddOidcServerApi(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }
}

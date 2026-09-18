using Microsoft.Extensions.DependencyInjection;

namespace AdminIdentity.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddAdminIdentityApi(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace AdminIdentity.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminIdentityApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}

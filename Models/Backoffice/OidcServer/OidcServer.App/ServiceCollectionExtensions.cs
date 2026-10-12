using Microsoft.Extensions.DependencyInjection;

namespace OidcServer.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOidcServerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<OidcServerService>();
        services.AddScoped<IOidcClientService>(sp => sp.GetRequiredService<OidcServerService>());
        services.AddScoped<IOidcScopeService>(sp => sp.GetRequiredService<OidcServerService>());
        return services;
    }
}

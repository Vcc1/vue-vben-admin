using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiGatewayApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}

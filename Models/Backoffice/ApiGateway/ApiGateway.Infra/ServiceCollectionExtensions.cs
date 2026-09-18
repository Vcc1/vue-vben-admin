using ApiGateway.App;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiGatewayInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IGatewayApiService, GatewayApiService>();
        services.AddScoped<IGatewayModuleService, GatewayModuleService>();
        services.AddScoped<IGatewayEndpointService, GatewayEndpointService>();
        services.AddScoped<IGatewayPermissionService, GatewayPermissionService>();
        services.AddScoped<IGatewayAuditService, GatewayAuditService>();
        services.AddScoped<IGatewayProxyService, GatewayProxyService>();
        services.AddHttpClient("Gateway");
        return services;
    }

    public static IApplicationBuilder UseGatewayProxy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<GatewayProxyMiddleware>();
    }
}

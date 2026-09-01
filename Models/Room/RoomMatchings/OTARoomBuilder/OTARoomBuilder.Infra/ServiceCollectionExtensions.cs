using Microsoft.Extensions.DependencyInjection;
using OTARoomBuilder;
using OTARoomBuilder.Contracts;
using SupplierStaticDataApp.Contracts;

namespace Infrastructure.OTARoomBuilder;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOtaRoomBuilderInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IOtaRoomBuilderRepository, OtaRoomBuilderRepository>();
        services.AddScoped<IAiRoomAuditRepository, OtaRoomAiAuditRepository>();
        services.AddScoped<ISupplierRoomBuildPlugin, EanSupplierRoomBuildPlugin>();
        services.AddScoped<ISupplierRoomBuildPlugin, WordRuleSupplierRoomBuildPlugin>();
        services.AddScoped<ISupplierRoomBuildPlugin>(services => new StaticDataSupplierRoomBuildPlugin(
            32,
            2,
            services.GetRequiredService<ISupplierStaticDataService>(),
            services.GetRequiredService<OTARoomBuilderMetrics>(),
            services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StaticDataSupplierRoomBuildPlugin>>()));
        services.AddScoped<ISupplierRoomBuildPlugin>(services => new StaticDataSupplierRoomBuildPlugin(
            1,
            3,
            services.GetRequiredService<ISupplierStaticDataService>(),
            services.GetRequiredService<OTARoomBuilderMetrics>(),
            services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StaticDataSupplierRoomBuildPlugin>>()));
        return services;
    }
}

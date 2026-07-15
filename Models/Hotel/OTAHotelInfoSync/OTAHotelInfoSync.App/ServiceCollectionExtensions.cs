using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OTAHotelInfoSync;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOTAHotelInfoSync(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = OTAHotelInfoSyncOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<OTAHotelInfoSyncOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IOTAHotelInfoSyncService, OTAHotelInfoSyncService>();
        services.AddHostedService<OTAHotelInfoSyncWorker>();
        return services;
    }
}

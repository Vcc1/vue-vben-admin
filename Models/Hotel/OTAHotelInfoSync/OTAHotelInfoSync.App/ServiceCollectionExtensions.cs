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
            .Validate(
                static options => options.AlertToUsers is { Length: > 0 }
                    && options.AlertToUsers.Any(static x => !string.IsNullOrWhiteSpace(x)),
                $"{sectionName}:AlertToUsers must contain at least one user.")
            .ValidateOnStart();

        services.AddScoped<IOTAHotelInfoSyncService, OTAHotelInfoSyncService>();
        services.AddSingleton<OTAHotelInfoSyncRetryExecutor>();
        services.AddHostedService<OTAHotelInfoSyncWorker>();
        return services;
    }
}

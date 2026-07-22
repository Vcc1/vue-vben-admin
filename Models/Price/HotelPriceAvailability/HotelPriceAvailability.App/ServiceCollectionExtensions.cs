using HotelPriceAvailability.App.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HotelPriceAvailability.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelPriceAvailabilityApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<HotelPriceAvailabilityOptions>, HotelPriceAvailabilityOptionsValidator>();
        services.AddOptions<HotelPriceAvailabilityOptions>()
            .Bind(configuration.GetSection(HotelPriceAvailabilityOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<HotelPriceAvailabilityMetrics>();
        services.AddSingleton<HotelPriceAvailabilityJob>();
        return services;
    }
}

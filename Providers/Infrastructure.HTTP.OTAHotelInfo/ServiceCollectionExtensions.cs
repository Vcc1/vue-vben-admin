using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.HTTP.OTAHotelInfo;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOtaHotelInfoHttp(
        this IServiceCollection services,
        Action<OtaHotelInfoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OtaHotelInfoOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddHttpClient<IOtaHotelInfoClient, OtaHotelInfoClient>();
        return services;
    }
}

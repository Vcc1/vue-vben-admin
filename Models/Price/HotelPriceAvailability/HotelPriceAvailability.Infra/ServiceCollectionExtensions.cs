using HotelPriceAvailability.App.Contracts;
using HotelPriceAvailability.Infra.Sql;
using HotelPriceAvailability.Infra.Sup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HotelPriceAvailability.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelPriceAvailabilityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IPriceAvailabilityTaskRepository, RoomMappingPriceAvailabilityTaskRepository>();
        services.AddHttpClient(nameof(SupPriceQueryGateway));
        services.AddSingleton<IPriceQueryGateway, SupPriceQueryGateway>();
        services.AddSingleton<IValidateOptions<SupPriceGatewayOptions>, SupPriceGatewayOptionsValidator>();
        services.AddOptions<SupPriceGatewayOptions>()
            .Bind(configuration.GetSection(SupPriceGatewayOptions.SectionName))
            .ValidateOnStart();
        return services;
    }
}

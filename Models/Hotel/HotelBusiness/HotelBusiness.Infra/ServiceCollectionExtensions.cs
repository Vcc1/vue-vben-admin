using HotelBusiness.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelBusiness.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelBusinessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HotelBusinessStorageOptions>()
            .Bind(configuration.GetSection(HotelBusinessStorageOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddSingleton<HotelBusinessStoragePaths>();
        services.AddSingleton<IHotelBusinessStore, FileHotelBusinessStore>();
        return services;
    }
}

using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Infra.Sql;
using HotelMatchingPipeline.Infra.ThirdParty;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelMatchingPipeline.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelMatchingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<RoomMappingHotelRepository>();
        services.AddSingleton<IHotelStarLevelIssueRecorder, HotelStarLevelIssueRecorder>();
        services.AddScoped<IHotelCleaningRepository>(provider => provider.GetRequiredService<RoomMappingHotelRepository>());
        services.AddScoped<IHotelMatchingRepository>(provider => provider.GetRequiredService<RoomMappingHotelRepository>());
        services.AddScoped<IHotelMatchDeliveryRepository>(provider => provider.GetRequiredService<RoomMappingHotelRepository>());

        services.AddOptions<ThirdPartyHotelMatchOptions>()
            .Bind(configuration.GetSection(ThirdPartyHotelMatchOptions.SectionName));
        services.AddHttpClient<IThirdPartyHotelMatchClient, ThirdPartyHotelMatchClient>();
        return services;
    }
}

using HotelResearch.App;
using HotelMatchingPipeline.Domain.Hotels;
using Microsoft.Extensions.DependencyInjection;

namespace HotelResearch.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelResearchInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IHotelResearchSeedReader, RoomMappingHotelResearchSeedReader>();
        services.AddScoped<IHotelBusinessDataReader, RoomMappingHotelBusinessDataReader>();
        services.AddSingleton<IHotelPhoneNormalizer, LibPhoneNumberNormalizer>();
        services.AddScoped<IHotelPhoneNormalizationService, HotelPhoneNormalizationService>();
        return services;
    }
}

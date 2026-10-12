using HotelMatchingPipeline.App.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Redis.HotelMatchingRedis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelMatchingRedis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<HotelMappingRedisStore>();
        services.AddSingleton<IHotelMappingRedisStore>(provider =>
            provider.GetRequiredService<HotelMappingRedisStore>());
        services.AddSingleton<IHotelMatchCache, HotelMatchRedisCache>();
        return services;
    }
}

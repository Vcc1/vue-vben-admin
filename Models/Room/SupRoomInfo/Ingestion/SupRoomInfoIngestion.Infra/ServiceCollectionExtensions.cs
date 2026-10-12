using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.SupRoomInfoIngestion;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSupRoomInfoIngestionInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CachePriceDiscoveryRedisContext>();
        services.AddSingleton<SupRoomInfoDBContext>();
        services.AddSingleton<ISupRoomInfoListReader, RedisSupRoomInfoListReader>();
        services.AddSingleton<ISupRoomInfoSetReader, RedisSupRoomInfoSetReader>();
        services.AddSingleton<ISupRoomInfoBatchWriter, SqlSupRoomInfoBatchWriter>();
        return services;
    }
}

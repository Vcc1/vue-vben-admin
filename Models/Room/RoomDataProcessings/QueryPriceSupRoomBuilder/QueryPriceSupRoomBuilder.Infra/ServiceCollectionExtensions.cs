using Microsoft.Extensions.DependencyInjection;
using QueryPriceSupRoomBuilder;
using Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery;

namespace Infrastructure.QueryPriceSupRoomBuilder;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQueryPriceSupRoomBuilderInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<SupRoomInfoDBContext>();
        services.AddScoped<IQueryPriceSupRoomBuildTaskRepository, QueryPriceSupRoomBuildTaskRepository>();
        services.AddSingleton<ISupRoomRedisPublisher, DirectPriceDiscoverySupRoomRedisPublisher>();
        return services;
    }
}

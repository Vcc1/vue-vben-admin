using Infrastructure.HTTP.SupStaticData.EAN;
using Infrastructures.RoomMatchingRedis;
using Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery;
using Infrastructure.Sql.MengTuAPI;
using Infrastructure.Sql.RoomMapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoomMatch.Core.RoomDictionary;

namespace RoomMatch.Core;

public static class RoomMatchInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRoomMatchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        Framework.Core.IOC.ServiceCollectionExpand.AddBusiness(
            services,
            [
                typeof(RoomMappingDbContext).Assembly,
                typeof(MengTuApiQueryService).Assembly,
                typeof(EANStaticDataContext).Assembly,
                typeof(RoomMappingRedis).Assembly,
                typeof(SupRoomInfoDBContext).Assembly,
                typeof(RoomDictionarySingleton).Assembly
            ]);

        return services;
    }
}

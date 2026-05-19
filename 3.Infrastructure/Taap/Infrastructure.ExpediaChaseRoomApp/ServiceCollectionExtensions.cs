using Infrastructure.Redis.SupPriceCahce;
using Infrastructure.Sql.SupCacheDB;
using Infrastructure.Web.Taap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpediaChaseRoomApp;

public static class ExpediaChaseRoomInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddExpediaChaseRoomInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddExpediaTaap(configuration);
        services.AddSingleton<SupPriceCahceRedis>();
        services.AddSingleton<IExpediaTaapClient, ProviderExpediaTaapClientAdapter>();
        services.AddSingleton<IExpediaTaapAuthenticationService, ProviderExpediaTaapAuthenticationServiceAdapter>();
        services.AddSingleton<IExpediaChaseRoomTaskStore, EfExpediaChaseRoomTaskStore>();
        services.AddSingleton<IExpediaChaseRoomPricePersister, ExpediaChaseRoomPricePersister>();
        services.AddSingleton<IExpediaTaapAccountSessionService, ExpediaTaapAccountSessionService>();
        services.AddSingleton<IExpediaTaapAccountQueryService, ExpediaTaapAccountQueryService>();
        services.AddHostedService<ExpediaTaapAccountWorkerManager>();
        services.AddHostedService<ExpediaChaseRoomDbPendingMetricsWorker>();

        Framework.Core.IOC.ServiceCollectionExpand.AddBusiness(
            services,
            [typeof(SupCacheDbContext).Assembly]);

        return services;
    }
}

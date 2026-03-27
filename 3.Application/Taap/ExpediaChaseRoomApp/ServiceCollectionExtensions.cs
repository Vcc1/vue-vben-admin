using Infrastructure.Redis.SupPriceCahce;
using Infrastructure.Sql.SupCacheDB;
using Infrastructure.Web.Taap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpediaChaseRoomApp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExpediaChaseRoomApp(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "ExpediaChaseRoomApp")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMemoryCache();
        services
            .AddOptions<ExpediaChaseRoomAppOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.GetExpediaChaseRoomHotelUrl),
                "ExpediaChaseRoomApp 缺少 GetExpediaChaseRoomHotelUrl 配置。")
            .ValidateOnStart();

        services.AddExpediaTaap(configuration);
        services.AddSingleton<ExpediaChaseRoomMetrics>();
        services.AddSingleton<ExpediaChaseRoomAppState>();
        services.AddSingleton<ExpediaChaseRoomQueue>();
        services.AddSingleton<IExpediaTaapPromoLabelProvider, ExpediaTaapPromoLabelProvider>();
        services.AddSingleton<IExpediaChaseRoomTaskStore, EfExpediaChaseRoomTaskStore>();
        services.AddSingleton<SupPriceCahceRedis>();
        services.AddSingleton<IExpediaChaseRoomPricePersister, ExpediaChaseRoomPricePersister>();
        services.AddSingleton<IExpediaTaapAccountSessionService, ExpediaTaapAccountSessionService>();
        services.AddSingleton<IExpediaTaapAccountQueryService, ExpediaTaapAccountQueryService>();

        services.AddHttpClient<IExpediaChaseRoomTaskSource, HttpExpediaChaseRoomTaskSource>();
        services.AddHttpClient<IExpediaChaseRoomResultSink, HttpExpediaChaseRoomResultSink>();
        services.AddHttpClient("ExpediaChaseRoomPriceCallback");

        services.AddHostedService<ExpediaChaseRoomAppWorker>();
        services.AddHostedService<ExpediaTaapAccountWorkerManager>();
        services.AddHostedService<ExpediaChaseRoomDbPendingMetricsWorker>();
        //services.AddHostedService<ExpediaChaseRoomMetricsWorker>();
        return services;
    }
}

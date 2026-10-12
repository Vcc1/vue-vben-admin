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

        services.AddSingleton<ExpediaChaseRoomMetrics>();
        services.AddSingleton<ExpediaChaseRoomAppState>();
        services.AddSingleton<ExpediaChaseRoomQueue>();
        services.AddSingleton<IExpediaTaapPromoLabelProvider, ExpediaTaapPromoLabelProvider>();

        services.AddHostedService<ExpediaChaseRoomAppWorker>();
        //services.AddHostedService<ExpediaChaseRoomMetricsWorker>();
        return services;
    }
}

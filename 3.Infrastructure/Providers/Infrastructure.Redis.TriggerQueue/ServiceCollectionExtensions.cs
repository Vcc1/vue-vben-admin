using Microsoft.Extensions.DependencyInjection;
using QueryPriceSupRoomBuilder;

namespace Infrastructure.Redis.TriggerQueue;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTriggerQueueRedis(this IServiceCollection services)
    {
        services.AddSingleton<TriggerQueueMetrics>();
        services.AddSingleton<TriggerQueueRedisContext>();
        services.AddSingleton<ITriggerQueueClient>(sp => sp.GetRequiredService<TriggerQueueRedisContext>());
        return services;
    }
}

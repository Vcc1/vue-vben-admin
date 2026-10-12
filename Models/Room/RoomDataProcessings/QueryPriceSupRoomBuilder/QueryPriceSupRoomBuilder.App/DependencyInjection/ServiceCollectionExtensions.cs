using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QueryPriceSupRoomBuilder;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQueryPriceSupRoomBuilder(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RoomBuildTask")
    {
        services
            .AddOptions<RoomBuildTaskOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RoomBuildTaskMetrics>();
        services.AddSingleton<SupplierQueryPriceRateLimiter>();
        services.AddSingleton<SupRoomCandidateSelector>();
        services.AddSingleton<RoomBuildTaskReaderStep>();
        services.AddSingleton<RoomBuildTaskQueryPriceStep>();
        services.AddSingleton<RoomBuildTaskPersistenceStep>();
        services.AddScoped<QueryPriceSupRoomBuildProcessor>();
        services.AddHostedService<RoomBuildTaskWorker>();
        services.AddHostedService<TriggerQueueWorker>();

        return services;
    }
}

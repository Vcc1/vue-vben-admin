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
        services.AddSingleton<SupRoomCandidateSelector>();
        services.AddScoped<QueryPriceSupRoomBuildProcessor>();
        services.AddHostedService<TriggerQueueWorker>();
        services.AddHostedService<RoomBuildTaskWorker>();

        return services;
    }
}

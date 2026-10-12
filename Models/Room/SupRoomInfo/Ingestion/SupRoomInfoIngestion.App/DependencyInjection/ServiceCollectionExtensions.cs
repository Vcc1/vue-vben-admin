using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SupRoomInfoIngestion;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSupRoomInfoIngestion(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "SupRoomInfoIngestion")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SupRoomInfoIngestionOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<SupRoomInfoIngestionState>();
        services.AddSingleton<SupRoomInfoIngestionQueue>();
        services.AddSingleton<SupRoomInfoIngestionMetrics>();
        services.AddSingleton<Contracts.IBedInfoRestoreStep, BedInfoRestoreStep>();
        services.AddSingleton<SupRoomInfoListSourceStep>();
        services.AddSingleton<SupRoomInfoSetSourceStep>();
        services.AddSingleton<SupRoomInfoSourceCoordinatorStep>();
        services.AddSingleton<SupRoomInfoNormalizeStep>();
        services.AddSingleton<SupRoomInfoBatchWriteStep>();
        services.AddSingleton<SupRoomInfoProduceStep>();
        services.AddSingleton<SupRoomInfoConsumeStep>();
        services.AddHostedService<SupRoomInfoIngestionWorker>();
        services.AddHostedService<SupRoomInfoIngestionMetricsWorker>();

        return services;
    }
}

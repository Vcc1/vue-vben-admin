using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OTARoomBuilder.AI;
using OTARoomBuilder.Contracts;

namespace OTARoomBuilder;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOtaRoomBuilder(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "OTARoomBuilder")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<OTARoomBuilderOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<RoomCreateOptions>()
            .Bind(configuration.GetSection("RoomCreate"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<OTARoomBuilderMetrics>();
        services.AddScoped<IAiRoomBuilder, MafAiRoomBuilder>();
        services.AddScoped<OTARoomBuilderProcessor>();
        services.AddHostedService<OTARoomBuilderWorker>();

        return services;
    }
}

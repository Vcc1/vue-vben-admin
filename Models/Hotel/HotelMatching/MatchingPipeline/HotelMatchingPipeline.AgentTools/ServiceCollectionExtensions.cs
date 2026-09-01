using AgentPlatform.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace HotelMatchingPipeline.AgentTools;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelMatchingAgentTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAgentToolContributor, HotelMatchingToolContributor>();
        return services;
    }
}

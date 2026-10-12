using AgentPlatform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPlatform.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentPlatformApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAgentConversationService, AgentConversationService>();
        return services;
    }
}

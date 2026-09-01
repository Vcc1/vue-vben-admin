using AgentPlatform.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPlatform.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentPlatformInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OpenAIAgentOptions>()
            .Bind(configuration.GetSection(OpenAIAgentOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptions<AgentStorageOptions>()
            .Bind(configuration.GetSection(AgentStorageOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddSingleton<AgentStoragePaths>();
        services.AddSingleton<FileAgentStorage>();
        services.AddSingleton<IAgentSessionStore>(provider => provider.GetRequiredService<FileAgentStorage>());
        services.AddSingleton<IAgentConversationStore>(provider => provider.GetRequiredService<FileAgentStorage>());
        services.AddSingleton<AgentConversationLockProvider>();
        services.AddScoped<IAgentRuntime, OpenAIAgentRuntime>();
        return services;
    }
}

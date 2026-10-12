using AgentPlatform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPlatform.Client;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddAgentPlatformClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureClient);

        return services.AddHttpClient<IAgentService, HttpAgentService>(configureClient);
    }
}

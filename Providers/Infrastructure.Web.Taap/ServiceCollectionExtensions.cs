using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Web.Taap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExpediaTaap(this IServiceCollection services, IConfiguration configuration, string sectionName = "ExpediaTAAP")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ExpediaTaapOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(
                static options => options.RequestsPerSecond > 0 && options.RequestQueueLimit >= 0,
                "ExpediaTAAP 限流配置无效。")
            .ValidateOnStart();

        services.AddSingleton<IExpediaTaapAuthenticationService, PlaywrightExpediaTaapAuthenticationService>();
        services.AddSingleton<ExpediaTaapRequestRateLimiter>();
        services.AddHttpClient<IExpediaTaapClient, ExpediaTaapClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<ExpediaTaapOptions>>().CurrentValue;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Max(options.RequestTimeoutSeconds, 1));
        });

        return services;
    }
}

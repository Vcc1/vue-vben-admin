using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrafanaAlertApp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGrafanaAlertApp(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "GrafanaAlert")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<GrafanaAlertOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<GrafanaAlertOptions>, GrafanaAlertOptionsValidator>();
        services.AddScoped<IGrafanaAlertForwarder, GrafanaAlertForwarder>();
        return services;
    }
}

using MetricsAndAlertsApp;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sql.MetricsAndAlerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsAndAlertsSqlProvider(this IServiceCollection services)
    {
        services.AddScoped<MetricsAndAlertsRepository>();
        services.AddScoped<IMetricsAndAlertsRepository>(sp => sp.GetRequiredService<MetricsAndAlertsRepository>());
        services.AddScoped<IMetricRulesRepository>(sp => sp.GetRequiredService<MetricsAndAlertsRepository>());
        return services;
    }
}

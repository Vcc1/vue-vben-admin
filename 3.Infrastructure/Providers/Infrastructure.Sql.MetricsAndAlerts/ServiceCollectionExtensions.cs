using MetricsAndAlertsApp;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sql.MetricsAndAlerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsAndAlertsSqlProvider(this IServiceCollection services)
    {
        services.AddScoped<IMetricsAndAlertsRepository, MetricsAndAlertsRepository>();
        return services;
    }
}

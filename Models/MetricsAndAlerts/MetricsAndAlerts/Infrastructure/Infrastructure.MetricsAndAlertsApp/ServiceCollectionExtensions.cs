using Infrastructure.HTTP.Prometheus;
using Infrastructure.HTTP.WorkWechatAlert;
using Infrastructure.Sql.MetricsAndAlerts;
using MetricsAndAlertsApp;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.MetricsAndAlertsApp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsAndAlertsInfrastructure(this IServiceCollection services)
    {
        services.AddPrometheusHttpProvider();
        services.AddMetricsAndAlertsSqlProvider();
        services.AddWorkWechatAlertHttpProvider();

        services.AddScoped<IAlertNotifier, WorkWechatAlertNotifier>();
        services.AddScoped<IUrlMonitorClient, HttpUrlMonitorClient>();
        services.AddSingleton<InMemoryUrlMonitorMetricStore>();
        services.AddSingleton<IUrlMonitorMetricRecorder>(sp => sp.GetRequiredService<InMemoryUrlMonitorMetricStore>());
        services.AddSingleton<IUrlMonitorMetricExporter>(sp => sp.GetRequiredService<InMemoryUrlMonitorMetricStore>());
        return services;
    }
}

using MetricsAndAlertsApp;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.HTTP.Prometheus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPrometheusHttpProvider(this IServiceCollection services)
    {
        services.AddHttpClient(PrometheusQueryClient.HttpClientName);
        services.AddScoped<IPrometheusQueryClient, PrometheusQueryClient>();
        return services;
    }
}

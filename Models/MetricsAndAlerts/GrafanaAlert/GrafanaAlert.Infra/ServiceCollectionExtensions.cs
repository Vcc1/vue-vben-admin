using GrafanaAlertApp;
using Infrastructure.HTTP.WorkWechatAlert;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.GrafanaAlertApp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGrafanaAlertInfrastructure(this IServiceCollection services)
    {
        services.AddWorkWechatAlertHttpProvider();
        services.AddScoped<IInternalAlertMessageSender, InternalAlertMessageSender>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Notification.App;
using Notification.Contracts;
using Notification.Infra.Channels;

namespace Notification.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddSingleton<INotificationHubPusher, NotificationHubPusher>();
        services.AddScoped<INotificationPublisher, NotificationDispatcher>();
        services.AddScoped<INotificationChannelHandler, InAppChannelHandler>();
        services.AddScoped<INotificationChannelHandler, WeChatWorkChannelHandler>();
        services.AddScoped<ICurrentUserIdAccessor, CurrentUserIdAccessor>();
        return services;
    }
}

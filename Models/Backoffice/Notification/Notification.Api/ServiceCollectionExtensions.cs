using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Notification.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddNotificationApi(this IMvcBuilder builder)
    {
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }

    public static IServiceCollection AddNotificationSignalR(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    public static WebApplication MapNotificationHub(this WebApplication app)
    {
        app.MapHub<App.NotificationHub>("/hubs/notification");
        return app;
    }
}

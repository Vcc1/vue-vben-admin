using Microsoft.Extensions.DependencyInjection;

namespace Notification.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<NotificationService>();
        services.AddScoped<UserNotificationService>();
        services.AddScoped<NotificationPreferenceService>();
        return services;
    }
}

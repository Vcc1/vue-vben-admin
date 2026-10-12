using Microsoft.Extensions.DependencyInjection;

namespace TaskScheduling.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaskSchedulingApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ScheduledTaskService>();
        return services;
    }
}

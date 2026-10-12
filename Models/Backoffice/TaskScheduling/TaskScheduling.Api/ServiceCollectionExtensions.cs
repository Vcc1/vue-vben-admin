using Microsoft.Extensions.DependencyInjection;

namespace TaskScheduling.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddTaskSchedulingApi(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        return builder;
    }
}

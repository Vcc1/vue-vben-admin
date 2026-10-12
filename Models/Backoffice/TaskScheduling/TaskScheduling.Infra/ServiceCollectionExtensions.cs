using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Quartz;
using TaskScheduling.App;
using TaskScheduling.Contracts;
using VBaseFramework.Core.EF;

namespace TaskScheduling.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaskSchedulingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration.GetSection(TaskSchedulingOptions.SectionName).Get<TaskSchedulingOptions>() ?? new();
        services.Configure<TaskSchedulingOptions>(configuration.GetSection(TaskSchedulingOptions.SectionName));

        var allowedKeys = settings.AllowedDatabaseKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var databases = services.Select(x => x.ServiceType)
            .Where(x => x.IsClass && !x.IsAbstract && typeof(DbContext).IsAssignableFrom(x))
            .Distinct()
            .Select(x => new { Type = x, Attribute = x.GetCustomAttribute<EFDbServiceAttribute>() })
            .Where(x => x.Attribute is not null && allowedKeys.Contains(x.Attribute.ConfigKey))
            .GroupBy(x => x.Attribute!.ConfigKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var item = group.First();
                settings.SqlTargets.TryGetValue(group.Key, out var target);
                var connectionName = target?.ExecutionConnectionStringName ?? string.Empty;
                return new RegisteredDatabase(group.Key, item.Type, connectionName,
                    !string.IsNullOrWhiteSpace(connectionName) && !string.IsNullOrWhiteSpace(configuration.GetConnectionString(connectionName)));
            }).ToArray();

        var handlers = handlerAssemblies.Distinct().SelectMany(x => x.GetTypes())
            .Where(x => x.IsClass && !x.IsAbstract && typeof(IScheduledTaskHandler).IsAssignableFrom(x))
            .Select(x => new { Type = x, Attribute = x.GetCustomAttribute<ScheduledTaskHandlerAttribute>() })
            .Where(x => x.Attribute is not null)
            .Select(x => new RegisteredHandler(x.Attribute!.Key, x.Attribute.DisplayName, x.Attribute.Description, x.Type))
            .ToArray();
        var duplicateHandler = handlers.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicateHandler is not null)
            throw new InvalidOperationException($"Duplicate scheduled task handler key '{duplicateHandler.Key}'.");
        foreach (var handler in handlers) services.AddScoped(handler.HandlerType);

        services.AddSingleton<IReadOnlyList<RegisteredDatabase>>(databases);
        services.AddSingleton<IReadOnlyList<RegisteredHandler>>(handlers);
        services.AddSingleton<TaskCatalogProvider>();
        services.AddSingleton<IScheduledTaskCatalogProvider>(provider => provider.GetRequiredService<TaskCatalogProvider>());
        services.AddSingleton<IScheduledTaskDefinitionValidator, ScheduledTaskDefinitionValidator>();
        services.AddScoped<ITaskSchedulingStore, TaskSchedulingStore>();
        services.AddScoped<IScheduledTaskExecutor, ScheduledTaskExecutor>();
        services.AddScoped<ProcessTaskExecutor>();
        services.AddScoped<SqlTaskExecutor>();
        services.AddScoped<ProgramTaskExecutor>();
        services.AddSingleton<ScheduledRunContextAccessor>();
        services.AddSingleton<RunFileLogStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ScheduledRunLoggerProvider>());
        services.AddScoped<IScheduledTaskLogReader, ScheduledTaskLogReader>();
        services.AddSingleton<ITaskSchedulerCoordinator, QuartzTaskCoordinator>();

        var metadataConnection = configuration.GetConnectionString(settings.MetadataConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{settings.MetadataConnectionStringName}' was not found for Quartz.");
        services.AddQuartz(quartz =>
        {
            quartz.SchedulerId = "AUTO";
            quartz.SchedulerName = "MengtuV.AdminScheduler";
            quartz.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UseSystemTextJsonSerializer();
                store.UseClustering();
                store.UseSqlServer(sqlServer =>
                {
                    sqlServer.ConnectionString = metadataConnection;
                    sqlServer.TablePrefix = "Scheduler_QRTZ_";
                });
            });
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        services.AddHostedService<TaskSchedulingReconciler>();
        return services;
    }
}

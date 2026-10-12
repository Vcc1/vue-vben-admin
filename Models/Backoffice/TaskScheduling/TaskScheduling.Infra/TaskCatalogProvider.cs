using TaskScheduling.App;
using TaskScheduling.Contracts;

namespace TaskScheduling.Infra;

internal sealed record RegisteredDatabase(string Key, Type DbContextType, string ExecutionConnectionStringName, bool HasExecutionConnection);
internal sealed record RegisteredHandler(string Key, string DisplayName, string? Description, Type HandlerType);

internal sealed class TaskCatalogProvider(
    IReadOnlyList<RegisteredDatabase> databases,
    IReadOnlyList<RegisteredHandler> handlers) : IScheduledTaskCatalogProvider
{
    public ScheduledTaskCatalog GetCatalog() => new(
        databases.Select(x => new ScheduledTaskDatabaseDescriptor(
            x.Key, x.DbContextType.FullName ?? x.DbContextType.Name, x.HasExecutionConnection)).ToArray(),
        handlers.Select(x => new ScheduledTaskHandlerDescriptor(
            x.Key, x.DisplayName, x.Description, x.HandlerType.FullName ?? x.HandlerType.Name)).ToArray());

    public RegisteredDatabase GetDatabase(string key) =>
        databases.SingleOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Scheduled SQL database '{key}' is not registered.");

    public RegisteredHandler GetHandler(string key) =>
        handlers.SingleOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Scheduled program handler '{key}' is not registered.");
}

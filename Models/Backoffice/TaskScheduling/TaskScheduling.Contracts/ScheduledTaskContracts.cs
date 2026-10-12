using System.Text.Json;

namespace TaskScheduling.Contracts;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScheduledTaskHandlerAttribute(string key, string displayName) : Attribute
{
    public string Key { get; } = key;
    public string DisplayName { get; } = displayName;
    public string? Description { get; init; }
}

public sealed record ScheduledTaskContext(
    Guid RunId,
    Guid TaskId,
    string TaskName,
    JsonElement Parameters,
    IServiceProvider Services);

public interface IScheduledTaskHandler
{
    Task ExecuteAsync(ScheduledTaskContext context, CancellationToken cancellationToken);
}

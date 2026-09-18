using System.Text.Json;

namespace TaskScheduling.App;

public static class ScheduledTaskTypes
{
    public const string Process = "Process";
    public const string Sql = "Sql";
    public const string Program = "Program";
    public static readonly string[] All = [Process, Sql, Program];
}

public static class ScheduledTaskStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string TimedOut = "TimedOut";
    public const string Cancelled = "Cancelled";
    public const string Interrupted = "Interrupted";
    public const string Skipped = "Skipped";
}

public sealed record ScheduledTaskInput(
    string Name,
    string? Description,
    string TaskType,
    string CronExpression,
    string TimeZoneId,
    string MisfirePolicy,
    bool IsEnabled,
    int TimeoutSeconds,
    int RetryCount,
    int RetryIntervalSeconds,
    bool RetryOnTimeout,
    JsonElement Configuration);

public sealed record ScheduledTaskDefinition(
    Guid Id,
    string Name,
    string? Description,
    string TaskType,
    string CronExpression,
    string TimeZoneId,
    string MisfirePolicy,
    bool IsEnabled,
    int TimeoutSeconds,
    int RetryCount,
    int RetryIntervalSeconds,
    bool RetryOnTimeout,
    JsonElement Configuration,
    string CreatedBy,
    DateTime CreatedAtUtc,
    string UpdatedBy,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record ScheduledTaskRun(
    Guid Id,
    Guid TaskId,
    Guid RootRunId,
    int AttemptNumber,
    string TriggerType,
    string Status,
    DateTime? ScheduledAtUtc,
    DateTime QueuedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? LastHeartbeatAtUtc,
    DateTime? FinishedAtUtc,
    DateTime? CancellationRequestedAtUtc,
    string? TriggeredBy,
    string? HostInstanceId,
    string? ResultSummary,
    string? ErrorSummary,
    string LogRelativePath,
    long LogLengthBytes,
    bool IsLogTruncated,
    bool IsLogDeleted);

public sealed record ScheduledTaskAudit(
    long Id,
    Guid? TaskId,
    Guid? RunId,
    string Action,
    string ActorType,
    string ActorId,
    string ActorName,
    string AuthenticationType,
    JsonElement? Before,
    JsonElement? After,
    DateTime OccurredAtUtc,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId);

public sealed record ScheduledTaskLogEntry(
    long Sequence,
    DateTime TimestampUtc,
    string Level,
    string Category,
    int EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    string? Exception);

public sealed record ScheduledTaskLogPage(
    Guid RunId,
    IReadOnlyList<ScheduledTaskLogEntry> Items,
    long NextSequence,
    bool IsComplete,
    bool IsAvailable);

public sealed record ScheduledTaskDatabaseDescriptor(
    string Key,
    string DbContextType,
    bool HasExecutionConnection);

public sealed record ScheduledTaskHandlerDescriptor(
    string Key,
    string DisplayName,
    string? Description,
    string TypeName);

public sealed record ScheduledTaskCatalog(
    IReadOnlyList<ScheduledTaskDatabaseDescriptor> Databases,
    IReadOnlyList<ScheduledTaskHandlerDescriptor> Handlers);

public sealed record RunRequest(string? Reason = null);
public sealed record SetTaskEnabledRequest(bool IsEnabled, string RowVersion);
public sealed record UpdateScheduledTaskRequest(ScheduledTaskInput Task, string RowVersion);

public sealed record QueuedRun(Guid RunId);

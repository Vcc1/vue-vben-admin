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

public sealed record TaskLogFilterRule(string CategoryPrefix, string MinLevel);

public sealed record TaskLogFilter(IReadOnlyList<TaskLogFilterRule> Rules);

public static class TaskLogFilterParser
{
    public static TaskLogFilter? Parse(JsonElement configuration)
    {
        if (configuration.ValueKind != JsonValueKind.Object
            || !configuration.TryGetProperty("logFilter", out var filter)
            || filter.ValueKind != JsonValueKind.Object
            || !filter.TryGetProperty("rules", out var rules)
            || rules.ValueKind != JsonValueKind.Array)
            return null;

        var parsed = new List<TaskLogFilterRule>();
        foreach (var rule in rules.EnumerateArray())
        {
            if (rule.ValueKind != JsonValueKind.Object) continue;
            var prefix = rule.TryGetProperty("categoryPrefix", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;
            var level = rule.TryGetProperty("minLevel", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() : null;
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(level)) continue;
            parsed.Add(new TaskLogFilterRule(prefix, level));
        }

        return parsed.Count > 0 ? new TaskLogFilter(parsed) : null;
    }
}

public sealed record QueuedRun(Guid RunId);

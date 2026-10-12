using System.Text.Json;
using System.Text.Json.Nodes;
using AdminIdentity.App;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Scheduling;
using Microsoft.EntityFrameworkCore;
using TaskScheduling.App;

namespace TaskScheduling.Infra;

internal sealed class TaskSchedulingStore(
    AdminDbContext dbContext,
    TimeProvider timeProvider) : ITaskSchedulingStore
{
    public async Task<IReadOnlyList<ScheduledTaskDefinition>> ListTasksAsync(CancellationToken cancellationToken) =>
        (await dbContext.ScheduledTasks.AsNoTracking().Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken)).Select(MapTask).ToArray();

    public async Task<ScheduledTaskDefinition?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ScheduledTasks.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == taskId && !x.IsDeleted, cancellationToken);
        return entity is null ? null : MapTask(entity);
    }

    public async Task<ScheduledTaskDefinition> CreateTaskAsync(ScheduledTaskInput input, CurrentActor actor, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new ScheduledTaskEntity
        {
            Id = Guid.CreateVersion7(),
            CreatedAtUtc = now,
            CreatedByActorId = actor.ActorId,
            CreatedByActorName = actor.ActorName,
            UpdatedAtUtc = now,
            UpdatedByActorId = actor.ActorId,
            UpdatedByActorName = actor.ActorName
        };
        Apply(entity, input);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ScheduledTasks.Add(entity);
        AddAudit("Create", entity.Id, null, null, input, actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapTask(entity);
    }

    public async Task<ScheduledTaskDefinition> UpdateTaskAsync(Guid taskId, ScheduledTaskInput input, string rowVersion, CurrentActor actor, CancellationToken cancellationToken)
    {
        var entity = await RequiredTaskAsync(taskId, cancellationToken);
        var before = MapTask(entity);
        SetRowVersion(entity, rowVersion);
        Apply(entity, input);
        entity.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        entity.UpdatedByActorId = actor.ActorId;
        entity.UpdatedByActorName = actor.ActorName;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddAudit("Update", taskId, null, before, input, actor);
        await SaveWithConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapTask(entity);
    }

    public async Task<ScheduledTaskDefinition> SetTaskEnabledAsync(Guid taskId, bool enabled, string rowVersion, CurrentActor actor, CancellationToken cancellationToken)
    {
        var entity = await RequiredTaskAsync(taskId, cancellationToken);
        var before = new { entity.IsEnabled };
        SetRowVersion(entity, rowVersion);
        entity.IsEnabled = enabled;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        entity.UpdatedByActorId = actor.ActorId;
        entity.UpdatedByActorName = actor.ActorName;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddAudit(enabled ? "Enable" : "Disable", taskId, null, before, new { entity.IsEnabled }, actor);
        await SaveWithConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapTask(entity);
    }

    public async Task DeleteTaskAsync(Guid taskId, string rowVersion, CurrentActor actor, CancellationToken cancellationToken)
    {
        var entity = await RequiredTaskAsync(taskId, cancellationToken);
        var before = MapTask(entity);
        SetRowVersion(entity, rowVersion);
        entity.IsDeleted = true;
        entity.IsEnabled = false;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        entity.UpdatedByActorId = actor.ActorId;
        entity.UpdatedByActorName = actor.ActorName;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        AddAudit("Delete", taskId, null, before, null, actor);
        await SaveWithConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ScheduledTaskRun> QueueRunAsync(
        Guid taskId,
        string triggerType,
        CurrentActor actor,
        Guid? rootRunId,
        int attemptNumber,
        DateTime? scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        var task = await RequiredTaskAsync(taskId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var runId = Guid.CreateVersion7();
        var run = new ScheduledTaskRunEntity
        {
            Id = runId,
            TaskId = taskId,
            RootRunId = rootRunId ?? runId,
            AttemptNumber = attemptNumber,
            TriggerType = triggerType,
            Status = ScheduledTaskStatuses.Queued,
            ScheduledAtUtc = scheduledAtUtc,
            QueuedAtUtc = now,
            TriggeredByActorType = actor.ActorType,
            TriggeredByActorId = actor.ActorId,
            TriggeredByActorName = actor.ActorName,
            AuthenticationType = actor.AuthenticationType,
            DefinitionSnapshotJson = JsonSerializer.Serialize(MapTask(task), JsonDefaults.Options),
            LogRelativePath = BuildLogRelativePath(now, runId)
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.ScheduledTaskRuns.Add(run);
        if (!string.Equals(triggerType, "Scheduled", StringComparison.Ordinal))
            AddAudit(triggerType, taskId, runId, null, new { run.Id, run.RootRunId, run.AttemptNumber }, actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapRun(run);
    }

    public async Task RequestCancellationAsync(Guid runId, CurrentActor actor, CancellationToken cancellationToken)
    {
        var run = await dbContext.ScheduledTaskRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Run was not found.");
        if (run.Status is not (ScheduledTaskStatuses.Queued or ScheduledTaskStatuses.Running))
            throw new InvalidOperationException("Only queued or running executions can be cancelled.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        run.CancellationRequestedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        AddAudit("Cancel", run.TaskId, run.Id, null, new { run.CancellationRequestedAtUtc }, actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledTaskRun>> ListRunsAsync(Guid? taskId, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.ScheduledTaskRuns.AsNoTracking();
        if (taskId.HasValue) query = query.Where(x => x.TaskId == taskId.Value);
        return (await query.OrderByDescending(x => x.QueuedAtUtc).Take(take).ToListAsync(cancellationToken))
            .Select(MapRun).ToArray();
    }

    public async Task<ScheduledTaskRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ScheduledTaskRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        return entity is null ? null : MapRun(entity);
    }

    public async Task<IReadOnlyList<ScheduledTaskAudit>> ListAuditsAsync(Guid? taskId, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.ScheduledTaskAudits.AsNoTracking();
        if (taskId.HasValue) query = query.Where(x => x.TaskId == taskId.Value);
        var items = await query.OrderByDescending(x => x.OccurredAtUtc).Take(take).ToListAsync(cancellationToken);
        return items.Select(x => new ScheduledTaskAudit(
            x.Id, x.TaskId, x.RunId, x.Action, x.ActorType, x.ActorId, x.ActorName,
            x.AuthenticationType, ParseOptional(x.BeforeJson), ParseOptional(x.AfterJson),
            x.OccurredAtUtc, x.IpAddress, x.UserAgent, x.CorrelationId)).ToArray();
    }

    private async Task<ScheduledTaskEntity> RequiredTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        await dbContext.ScheduledTasks.SingleOrDefaultAsync(x => x.Id == taskId && !x.IsDeleted, cancellationToken)
        ?? throw new KeyNotFoundException("Scheduled task was not found.");

    private void AddAudit(string action, Guid? taskId, Guid? runId, object? before, object? after, CurrentActor actor)
    {
        dbContext.ScheduledTaskAudits.Add(new ScheduledTaskAuditEntity
        {
            TaskId = taskId,
            RunId = runId,
            Action = action,
            ActorType = actor.ActorType,
            ActorId = actor.ActorId,
            ActorName = actor.ActorName,
            AuthenticationType = actor.AuthenticationType,
            BeforeJson = AuditJson(before),
            AfterJson = AuditJson(after),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            CorrelationId = actor.CorrelationId
        });
    }

    private async Task SaveWithConcurrencyAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("The scheduled task was changed by another user. Reload and retry.", exception);
        }
    }

    private void SetRowVersion(ScheduledTaskEntity entity, string rowVersion)
    {
        try { dbContext.Entry(entity).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion); }
        catch (FormatException exception) { throw new ArgumentException("RowVersion is invalid.", exception); }
    }

    private static void Apply(ScheduledTaskEntity entity, ScheduledTaskInput input)
    {
        entity.Name = input.Name.Trim();
        entity.Description = input.Description?.Trim();
        entity.TaskType = input.TaskType;
        entity.CronExpression = input.CronExpression.Trim();
        entity.TimeZoneId = input.TimeZoneId;
        entity.MisfirePolicy = input.MisfirePolicy;
        entity.IsEnabled = input.IsEnabled;
        entity.TimeoutSeconds = input.TimeoutSeconds;
        entity.RetryCount = input.RetryCount;
        entity.RetryIntervalSeconds = input.RetryIntervalSeconds;
        entity.RetryOnTimeout = input.RetryOnTimeout;
        entity.ConfigurationJson = input.Configuration.GetRawText();
    }

    internal static ScheduledTaskDefinition MapTask(ScheduledTaskEntity x) => new(
        x.Id, x.Name, x.Description, x.TaskType, x.CronExpression, x.TimeZoneId,
        x.MisfirePolicy, x.IsEnabled, x.TimeoutSeconds, x.RetryCount,
        x.RetryIntervalSeconds, x.RetryOnTimeout, Parse(x.ConfigurationJson),
        x.CreatedByActorName, x.CreatedAtUtc, x.UpdatedByActorName, x.UpdatedAtUtc,
        Convert.ToBase64String(x.RowVersion));

    internal static ScheduledTaskRun MapRun(ScheduledTaskRunEntity x) => new(
        x.Id, x.TaskId, x.RootRunId, x.AttemptNumber, x.TriggerType, x.Status,
        AsUtc(x.ScheduledAtUtc), AsUtc(x.QueuedAtUtc), AsUtc(x.StartedAtUtc), AsUtc(x.LastHeartbeatAtUtc),
        AsUtc(x.FinishedAtUtc), AsUtc(x.CancellationRequestedAtUtc), x.TriggeredByActorName,
        x.HostInstanceId, x.ResultSummary, x.ErrorSummary, x.LogRelativePath,
        x.LogLengthBytes, x.IsLogTruncated, x.LogDeletedAtUtc.HasValue);

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static JsonElement? ParseOptional(string? json) => json is null ? null : Parse(json);
    private static string BuildLogRelativePath(DateTime utc, Guid runId) =>
        Path.Combine(utc.ToString("yyyy"), utc.ToString("MM"), utc.ToString("dd"), $"{runId:N}.ndjson");

    private static string? AuditJson(object? value)
    {
        if (value is null) return null;
        var node = JsonSerializer.SerializeToNode(value, JsonDefaults.Options);
        Redact(node);
        return node?.ToJsonString(JsonDefaults.Options);
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (property.Key.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Contains("token", StringComparison.OrdinalIgnoreCase)
                    || property.Key.Contains("connectionString", StringComparison.OrdinalIgnoreCase))
                    obj[property.Key] = "[REDACTED_SECRET]";
                else Redact(property.Value);
            }
        }
        else if (node is JsonArray array)
            foreach (var item in array) Redact(item);
    }
}

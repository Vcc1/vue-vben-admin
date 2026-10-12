using AdminIdentity.App;

namespace TaskScheduling.App;

public interface ITaskSchedulingStore
{
    Task<IReadOnlyList<ScheduledTaskDefinition>> ListTasksAsync(CancellationToken cancellationToken);
    Task<ScheduledTaskDefinition?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task<ScheduledTaskDefinition> CreateTaskAsync(ScheduledTaskInput input, CurrentActor actor, CancellationToken cancellationToken);
    Task<ScheduledTaskDefinition> UpdateTaskAsync(Guid taskId, ScheduledTaskInput input, string rowVersion, CurrentActor actor, CancellationToken cancellationToken);
    Task<ScheduledTaskDefinition> SetTaskEnabledAsync(Guid taskId, bool enabled, string rowVersion, CurrentActor actor, CancellationToken cancellationToken);
    Task DeleteTaskAsync(Guid taskId, string rowVersion, CurrentActor actor, CancellationToken cancellationToken);
    Task<ScheduledTaskRun> QueueRunAsync(Guid taskId, string triggerType, CurrentActor actor, Guid? rootRunId, int attemptNumber, DateTime? scheduledAtUtc, CancellationToken cancellationToken);
    Task RequestCancellationAsync(Guid runId, CurrentActor actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduledTaskRun>> ListRunsAsync(Guid? taskId, int take, CancellationToken cancellationToken);
    Task<ScheduledTaskRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduledTaskAudit>> ListAuditsAsync(Guid? taskId, int take, CancellationToken cancellationToken);
}

public interface ITaskSchedulerCoordinator
{
    Task SynchronizeTaskAsync(ScheduledTaskDefinition task, CancellationToken cancellationToken);
    Task RemoveTaskAsync(Guid taskId, CancellationToken cancellationToken);
    Task TriggerRunAsync(Guid taskId, Guid runId, DateTime? fireAtUtc, CancellationToken cancellationToken);
    Task InterruptRunAsync(Guid runId, CancellationToken cancellationToken);
}

public interface IScheduledTaskDefinitionValidator
{
    void Validate(ScheduledTaskInput input);
}

public interface IScheduledTaskCatalogProvider
{
    ScheduledTaskCatalog GetCatalog();
}

public interface IScheduledTaskLogReader
{
    Task<ScheduledTaskLogPage> ReadAsync(Guid runId, long afterSequence, int take, CancellationToken cancellationToken);
}

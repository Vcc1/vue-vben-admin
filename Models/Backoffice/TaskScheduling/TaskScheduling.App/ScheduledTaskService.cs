using AdminIdentity.App;

namespace TaskScheduling.App;

public sealed class ScheduledTaskService(
    ITaskSchedulingStore store,
    ITaskSchedulerCoordinator coordinator,
    IScheduledTaskDefinitionValidator validator,
    IScheduledTaskCatalogProvider catalogProvider,
    IScheduledTaskLogReader logReader,
    ICurrentActorAccessor actorAccessor)
{
    public Task<IReadOnlyList<ScheduledTaskDefinition>> ListTasksAsync(CancellationToken cancellationToken) =>
        store.ListTasksAsync(cancellationToken);

    public Task<ScheduledTaskDefinition?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        store.GetTaskAsync(taskId, cancellationToken);

    public async Task<ScheduledTaskDefinition> CreateTaskAsync(ScheduledTaskInput input, CancellationToken cancellationToken)
    {
        validator.Validate(input);
        var task = await store.CreateTaskAsync(input, actorAccessor.Current, cancellationToken);
        await coordinator.SynchronizeTaskAsync(task, cancellationToken);
        return task;
    }

    public async Task<ScheduledTaskDefinition> UpdateTaskAsync(Guid taskId, UpdateScheduledTaskRequest request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Task);
        var task = await store.UpdateTaskAsync(taskId, request.Task, request.RowVersion, actorAccessor.Current, cancellationToken);
        await coordinator.SynchronizeTaskAsync(task, cancellationToken);
        return task;
    }

    public async Task<ScheduledTaskDefinition> SetEnabledAsync(Guid taskId, SetTaskEnabledRequest request, CancellationToken cancellationToken)
    {
        var task = await store.SetTaskEnabledAsync(taskId, request.IsEnabled, request.RowVersion, actorAccessor.Current, cancellationToken);
        await coordinator.SynchronizeTaskAsync(task, cancellationToken);
        return task;
    }

    public async Task DeleteAsync(Guid taskId, string rowVersion, CancellationToken cancellationToken)
    {
        await store.DeleteTaskAsync(taskId, rowVersion, actorAccessor.Current, cancellationToken);
        await coordinator.RemoveTaskAsync(taskId, cancellationToken);
    }

    public async Task<QueuedRun> RunNowAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var run = await store.QueueRunAsync(taskId, "RunNow", actorAccessor.Current, null, 1, null, cancellationToken);
        await coordinator.TriggerRunAsync(taskId, run.Id, null, cancellationToken);
        return new QueuedRun(run.Id);
    }

    public async Task<QueuedRun> RetryAsync(Guid runId, CancellationToken cancellationToken)
    {
        var original = await store.GetRunAsync(runId, cancellationToken)
            ?? throw new KeyNotFoundException("Run was not found.");
        var retry = await store.QueueRunAsync(
            original.TaskId,
            "ManualRetry",
            actorAccessor.Current,
            original.RootRunId,
            original.AttemptNumber + 1,
            null,
            cancellationToken);
        await coordinator.TriggerRunAsync(retry.TaskId, retry.Id, null, cancellationToken);
        return new QueuedRun(retry.Id);
    }

    public async Task CancelAsync(Guid runId, CancellationToken cancellationToken)
    {
        await store.RequestCancellationAsync(runId, actorAccessor.Current, cancellationToken);
        await coordinator.InterruptRunAsync(runId, cancellationToken);
    }

    public Task<IReadOnlyList<ScheduledTaskRun>> ListRunsAsync(Guid? taskId, int take, CancellationToken cancellationToken) =>
        store.ListRunsAsync(taskId, Math.Clamp(take, 1, 200), cancellationToken);

    public Task<ScheduledTaskRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken) =>
        store.GetRunAsync(runId, cancellationToken);

    public Task<IReadOnlyList<ScheduledTaskAudit>> ListAuditsAsync(Guid? taskId, int take, CancellationToken cancellationToken) =>
        store.ListAuditsAsync(taskId, Math.Clamp(take, 1, 200), cancellationToken);

    public Task<ScheduledTaskLogPage> ReadLogsAsync(Guid runId, long afterSequence, int take, CancellationToken cancellationToken) =>
        logReader.ReadAsync(runId, Math.Max(0, afterSequence), Math.Clamp(take, 1, 1000), cancellationToken);

    public ScheduledTaskCatalog GetCatalog() => catalogProvider.GetCatalog();
}

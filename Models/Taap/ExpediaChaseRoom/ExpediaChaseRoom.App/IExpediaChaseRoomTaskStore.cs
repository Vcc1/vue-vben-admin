namespace ExpediaChaseRoomApp;

public interface IExpediaChaseRoomTaskStore
{
    Task UnlockAllProcessingTasksAsync(CancellationToken cancellationToken = default);

    Task SyncTasksAsync(IReadOnlyList<ExpediaChaseRoomTask> tasks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpediaChaseRoomTask>> LeasePendingTasksAsync(
        int take,
        string leaseOwner,
        CancellationToken cancellationToken = default);

    Task AssignProcessingTaskAsync(int taskId, string completedBy, CancellationToken cancellationToken = default);

    Task ReleaseTaskAsync(int taskId, CancellationToken cancellationToken = default);

    Task MarkCompletedAndLogAsync(
        ExpediaChaseRoomTask task,
        string completedBy,
        bool hasPrice,
        CancellationToken cancellationToken = default);

    Task MarkOfflineAsync(ExpediaChaseRoomTask task, CancellationToken cancellationToken = default);
}

namespace ExpediaChaseRoomApp;

public interface IExpediaChaseRoomTaskSource
{
    Task<IReadOnlyList<ExpediaChaseRoomTask>> FetchPendingTasksAsync(CancellationToken cancellationToken = default);
}
namespace ExpediaChaseRoomApp;

public sealed record ExpediaTaapAccountWorkerContext
{
    public required int AccountId { get; init; }

    public string AccountName { get; init; } = string.Empty;

    public string SigninEmailAddress { get; init; } = string.Empty;

    public int WorkerSlot { get; init; } = 1;

    public string WorkerKey => $"{AccountName}";
}

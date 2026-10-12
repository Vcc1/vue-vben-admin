namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomQueryErrorCallbackInput
{
    public required ExpediaChaseRoomTask Task { get; init; }

    public required string ErrorMessage { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
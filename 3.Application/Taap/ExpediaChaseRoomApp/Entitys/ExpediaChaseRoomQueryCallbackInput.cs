using Infrastructure.Web.Taap;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomQueryCallbackInput
{
    public required ExpediaChaseRoomTask Task { get; init; }

    public required ExpediaTaapPriceQueryResult Result { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
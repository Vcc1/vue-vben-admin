namespace HotelMatchingPipeline.App.Contracts;

public sealed record HotelMatchDeliveryCommand(
    int SupId,
    string SHotelCode,
    string MTHotelCode,
    long SourceDataVersion,
    string NormalizedDataHash,
    int CleanVersion,
    int MatchingVersion,
    string IdempotencyKey);

public sealed record HotelMatchDeliveryReceipt(bool Success, string? Response, string? Error);

public interface IHotelMatchDeliveryRepository
{
    Task<bool> TryCreateAsync(HotelMatchDeliveryCommand command, CancellationToken cancellationToken);

    Task MarkSendingAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task MarkCompletedAsync(
        string idempotencyKey,
        HotelMatchDeliveryReceipt receipt,
        DateTimeOffset? nextRetryTime,
        CancellationToken cancellationToken);
}

public interface IThirdPartyHotelMatchClient
{
    Task<HotelMatchDeliveryReceipt> DeliverAsync(
        HotelMatchDeliveryCommand command,
        CancellationToken cancellationToken);
}

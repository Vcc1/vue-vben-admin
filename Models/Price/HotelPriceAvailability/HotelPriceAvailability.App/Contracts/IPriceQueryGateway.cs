using HotelPriceAvailability.Domain;

namespace HotelPriceAvailability.App.Contracts;

public sealed record PriceQueryBatch(
    int SupId,
    PriceLookupMode LookupMode,
    IReadOnlyList<PriceCheckTask> Tasks,
    PriceSearchCombination Combination);

public sealed record PriceQueryOutcome(
    bool IsSuccessful,
    IReadOnlyList<int> HitTaskIds,
    string? Error = null)
{
    public static PriceQueryOutcome Success(IReadOnlyList<int>? hitTaskIds = null) =>
        new(true, hitTaskIds ?? []);

    public static PriceQueryOutcome Failure(string error) => new(false, [], error);
}

public interface IPriceQueryGateway
{
    Task<PriceQueryOutcome> QueryAsync(
        PriceQueryBatch batch,
        CancellationToken cancellationToken);
}

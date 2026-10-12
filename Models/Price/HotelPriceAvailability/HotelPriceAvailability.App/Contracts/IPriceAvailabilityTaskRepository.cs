using HotelPriceAvailability.Domain;

namespace HotelPriceAvailability.App.Contracts;

public interface IPriceAvailabilityTaskRepository
{
    Task<IReadOnlyList<PriceCheckTask>> LoadPendingAsync(
        string batchNo,
        CancellationToken cancellationToken);

    Task MarkPricedAsync(
        IReadOnlyList<PriceHit> hits,
        CancellationToken cancellationToken);
}

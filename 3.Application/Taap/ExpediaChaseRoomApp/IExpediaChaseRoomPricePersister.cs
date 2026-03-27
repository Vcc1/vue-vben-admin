using Infrastructure.Web.Taap;

namespace ExpediaChaseRoomApp;

public interface IExpediaChaseRoomPricePersister
{
    Task<ExpediaChaseRoomPricePersistResult> PersistAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaChaseRoomTask task,
        ExpediaTaapPriceQueryResult result,
        CancellationToken cancellationToken = default);
}

public readonly record struct ExpediaChaseRoomPricePersistResult(bool HasPrice);

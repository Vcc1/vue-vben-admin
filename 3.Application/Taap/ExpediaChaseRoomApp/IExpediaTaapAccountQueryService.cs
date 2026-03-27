using Infrastructure.Web.Taap;

namespace ExpediaChaseRoomApp;

public interface IExpediaTaapAccountQueryService
{
    Task<ExpediaTaapPriceQueryResult> QueryPricesAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaTaapPriceQueryRequest request,
        CancellationToken cancellationToken = default);
}

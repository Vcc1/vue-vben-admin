namespace Infrastructure.Web.Taap;

public interface IExpediaTaapClient
{
    Task<ExpediaTaapPriceQueryResult> QueryPricesAsync(
        int accountId,
        ExpediaTaapLoginState loginState,
        ExpediaTaapPriceQueryRequest request,
        CancellationToken cancellationToken = default);
}

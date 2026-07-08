namespace QueryPriceSupRoomBuilder;

public interface IQueryPriceClient
{
    Task<QueryPriceResponse?> QueryPriceAsync(QueryPriceRequest request, CancellationToken cancellationToken);
}

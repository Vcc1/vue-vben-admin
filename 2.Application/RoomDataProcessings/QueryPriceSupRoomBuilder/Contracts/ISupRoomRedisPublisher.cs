namespace QueryPriceSupRoomBuilder;

public interface ISupRoomRedisPublisher
{
    Task<long> PushAsync(IReadOnlyList<SupRoomRedisMessage> rooms, CancellationToken cancellationToken);
}

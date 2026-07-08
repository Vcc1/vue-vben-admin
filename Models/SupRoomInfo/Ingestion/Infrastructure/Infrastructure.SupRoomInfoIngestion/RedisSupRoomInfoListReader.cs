namespace Infrastructure.SupRoomInfoIngestion;

public sealed class RedisSupRoomInfoListReader : ISupRoomInfoListReader
{
    readonly SupRoomInfoDBContext _redis;

    public RedisSupRoomInfoListReader(SupRoomInfoDBContext redis)
    {
        _redis = redis;
    }

    public Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(int maxSize, CancellationToken cancellationToken)
    {
        var items = _redis.GetSupRoomInfo(maxSize, cancellationToken);
        return Task.FromResult<IReadOnlyList<TempSupRoomInfo2>>(items);
    }

    public Task<long> GetPendingCountAsync(string listKey, CancellationToken cancellationToken)
    {
        var count = _redis.GetListLength(listKey);
        return Task.FromResult(count);
    }
}

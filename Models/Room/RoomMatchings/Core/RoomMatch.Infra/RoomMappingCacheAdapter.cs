using Infrastructures.RoomMatchingRedis;
using RoomMatch.App;

namespace RoomMatch.Infra;

public sealed class RoomMappingCacheAdapter : IRoomMappingCache
{
    private readonly RoomMappingRedis _redis;

    public RoomMappingCacheAdapter(RoomMappingRedis redis)
    {
        _redis = redis;
    }

    public async Task<object?> GetCacheAsync(string mtHotelCode)
    {
        return await _redis.GetSourceAsync(mtHotelCode);
    }
}

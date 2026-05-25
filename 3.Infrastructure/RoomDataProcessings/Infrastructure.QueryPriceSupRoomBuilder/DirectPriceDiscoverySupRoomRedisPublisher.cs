using Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery;
using Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery.Entitys;
using QueryPriceSupRoomBuilder;

namespace Infrastructure.QueryPriceSupRoomBuilder;

public sealed class DirectPriceDiscoverySupRoomRedisPublisher : ISupRoomRedisPublisher
{
    readonly SupRoomInfoDBContext _redis;

    public DirectPriceDiscoverySupRoomRedisPublisher(SupRoomInfoDBContext redis)
    {
        _redis = redis;
    }

    public Task<long> PushAsync(IReadOnlyList<SupRoomRedisMessage> rooms, CancellationToken cancellationToken)
    {
        return _redis.PushSupRoomModelsAsync(rooms.Select(Map).ToArray(), cancellationToken);
    }

    static SupRoomModel Map(SupRoomRedisMessage room)
    {
        return new SupRoomModel
        {
            RoomName = room.RoomName,
            RoomEnName = room.RoomEnName,
            HotelCode = room.HotelCode,
            MTHotelCode = room.MTHotelCode,
            RoomCode = room.RoomCode,
            BedType = room.BedType,
            BedInfo = room.BedInfo,
            BedInfoKey = room.BedInfoKey,
            AcreageFrom = room.AcreageFrom,
            AcreageTo = room.AcreageTo,
            SmokeType = room.SmokeType,
            MaxAdt = room.MaxAdt,
            SupId = room.SupId,
            CreateTime = room.CreateTime
        };
    }
}

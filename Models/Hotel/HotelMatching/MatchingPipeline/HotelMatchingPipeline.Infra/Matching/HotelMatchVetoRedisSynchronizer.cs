using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.App.Models;
using Infrastructure.Redis.HotelMatchingRedis;

namespace HotelMatchingPipeline.Infra;

public sealed class HotelMatchVetoRedisSynchronizer : IHotelMatchVetoRedisSynchronizer
{
    readonly IHotelMappingRedisStore _store;

    public HotelMatchVetoRedisSynchronizer(IHotelMappingRedisStore store)
    {
        _store = store;
    }

    public Task RemoveAsync(HotelMatchVetoSyncItem veto, CancellationToken cancellationToken)
        => _store.RemoveSupplierHotelAsync(
            veto.MTHotelCode,
            veto.SupId,
            veto.SHotelCode,
            cancellationToken);
}

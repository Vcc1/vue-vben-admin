using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.Infra.Testing;

public sealed class EmptyHotelRegionResolver : IHotelRegionResolver
{
    public RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default)
        => RegionResolution.NotFound();
}

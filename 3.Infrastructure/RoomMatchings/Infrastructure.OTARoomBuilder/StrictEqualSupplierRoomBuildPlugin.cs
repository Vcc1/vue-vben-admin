using OTARoomBuilder;
using RoomMatch.Core.RoomMatchingStrategies;

namespace Infrastructure.OTARoomBuilder;

public sealed class StrictEqualSupplierRoomBuildPlugin : BaseSupplierRoomBuildPlugin<StrictEqualMatcher>
{
    readonly int _supId;
    readonly int _priority;

    public StrictEqualSupplierRoomBuildPlugin(int supId, int priority)
    {
        _supId = supId;
        _priority = priority;
    }

    public override int SupId => _supId;

    public override int Priority => _priority;

    public override Task<SupplierRoomEnrichmentResult> EnrichAsync(
        SupplierRoomBuildContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(SupplierRoomEnrichmentResult.NotSupported);
    }
}

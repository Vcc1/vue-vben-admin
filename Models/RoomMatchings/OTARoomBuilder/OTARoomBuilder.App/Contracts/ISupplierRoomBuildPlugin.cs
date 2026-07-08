using Infrastructure.Sql.MengTuSup.Entitys;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomMatchingStrategies;

namespace OTARoomBuilder.Contracts;

public interface ISupplierRoomBuildPlugin
{
    int SupId { get; }

    int Priority { get; }

    bool CanEnrich(SupRoomInfo room);

    IRoomMatcher ResolveMatcher(IServiceProvider services);

    Task<SupplierRoomEnrichmentResult> EnrichAsync(
        SupplierRoomBuildContext context,
        CancellationToken cancellationToken);
}

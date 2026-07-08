using Microsoft.Extensions.DependencyInjection;
using OTARoomBuilder;
using OTARoomBuilder.Contracts;
using RoomMatch.Core.RoomMatchingStrategies;

namespace Infrastructure.OTARoomBuilder;

public abstract class BaseSupplierRoomBuildPlugin<TMatcher> : ISupplierRoomBuildPlugin
    where TMatcher : class, IRoomMatcher
{
    public abstract int SupId { get; }

    public abstract int Priority { get; }

    public virtual bool CanEnrich(Infrastructure.Sql.MengTuSup.Entitys.SupRoomInfo room) => false;

    public virtual IRoomMatcher ResolveMatcher(IServiceProvider services)
    {
        return services.GetRequiredService<TMatcher>();
    }

    public abstract Task<SupplierRoomEnrichmentResult> EnrichAsync(
        SupplierRoomBuildContext context,
        CancellationToken cancellationToken);
}

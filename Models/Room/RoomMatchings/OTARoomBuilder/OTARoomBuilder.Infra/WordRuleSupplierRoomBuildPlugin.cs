using Infrastructure.Sql.MengTuSup.Entitys;
using OTARoomBuilder;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomMatchingStrategies;

namespace Infrastructure.OTARoomBuilder;

public sealed class WordRuleSupplierRoomBuildPlugin : BaseSupplierRoomBuildPlugin<StrictEqualMatcher>
{
    static readonly IReadOnlyDictionary<int, int> WordPersonMap = new Dictionary<int, int>
    {
        [2771] = 8,
        [9406] = 1,
        [37501] = 1,
        [3019] = 1,
        [37434] = 1,
        [2849] = 1,
        [3180] = 7,
        [21725] = 6,
        [3347] = 5,
        [3277] = 4,
        [3217] = 3
    };

    public override int SupId => 75;

    public override int Priority => 4;

    public override bool CanEnrich(SupRoomInfo room) => true;

    public override Task<SupplierRoomEnrichmentResult> EnrichAsync(
        SupplierRoomBuildContext context,
        CancellationToken cancellationToken)
    {
        var maxAdt = GetMaxAllowedForRoomWord(context.RoomContext);
        if (maxAdt <= 0)
        {
            return Task.FromResult(SupplierRoomEnrichmentResult.NotSupported);
        }

        return Task.FromResult(new SupplierRoomEnrichmentResult
        {
            Success = true,
            MaxAdt = maxAdt
        });
    }

    static int GetMaxAllowedForRoomWord(RoomContext? room)
    {
        if (room?.RoomNameWords is null || room.RoomNameWords.Count == 0)
        {
            return 0;
        }

        foreach (var word in room.RoomNameWords)
        {
            var id = word?.WordAttr?.Id ?? 0;
            if (id > 0 && WordPersonMap.TryGetValue(id, out var person))
            {
                return person;
            }
        }

        return 2;
    }
}

using RoomMatch.Core.RoomAnalysis;

namespace RoomMatch.Core.RoomMatchingStrategies
{
    public interface IRoomMatcher
    {
        string Name { get; }

        /// <summary>
        /// 单个 OTA 房型比对
        /// </summary>
        MatchResult Match(RoomContext sup, RoomContext ota);

        /// <summary>
        /// 多个 OTA 房型批量比对
        /// </summary>
        IEnumerable<MatchResult> Match(RoomContext sup, List<RoomContext> otaList);
    }
}

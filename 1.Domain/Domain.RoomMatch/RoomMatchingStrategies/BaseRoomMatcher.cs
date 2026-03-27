using RoomMatch.Core.RoomAnalysis;

namespace RoomMatch.Core.RoomMatchingStrategies
{
    public abstract class BaseRoomMatcher : IRoomMatcher
    {
        public abstract string Name { get; }

        public abstract MatchResult Match(RoomContext sup, RoomContext ota);

        public virtual IEnumerable<MatchResult> Match(RoomContext sup, List<RoomContext> otaList)
        {
            foreach (var ota in otaList)
            {
                var result = Match(sup, ota);
                if (result.IsMatch)
                {
                    yield return result;
                }
            }
        }
    }
}

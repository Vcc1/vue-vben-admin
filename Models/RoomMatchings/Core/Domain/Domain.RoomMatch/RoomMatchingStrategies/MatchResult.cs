using RoomMatch.Core.RoomAnalysis;

namespace RoomMatch.Core.RoomMatchingStrategies
{
    public struct MatchResult
    {
        /// <summary> 是否匹配成功 </summary>
        public bool IsMatch { get; set; }
        /// <summary> 比对器名（可选）</summary>
        public string MatcherName { get; set; }
        public RoomContext SupRoom { get; init; }
        public RoomContext OtaRoom { get; init; }

        /// <summary> 匹配类型（可选）</summary>
        public int MatchType { get; set; }

        /// <summary> 匹配得分（可选）</summary>
        public decimal? MatchScore { get; set; }

        public static MatchResult NotMatched(string matcherName)
            => new MatchResult { IsMatch = false, MatcherName = matcherName };

        public static MatchResult Matched(string matcherName, int type = 0, decimal? score = null)
            => new MatchResult { IsMatch = true, MatchType = type, MatchScore = score, MatcherName = matcherName };
    }
}

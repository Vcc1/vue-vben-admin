using Framework.Core.IOC;
using Microsoft.Extensions.Logging;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;

namespace RoomMatch.Core.RoomMatchingStrategies
{
    /// <summary>
    /// 全词语去符号顺序匹配
    /// </summary>
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class FullSequenceMatcher : BaseRoomMatcher
    {

        ILogger<FullSequenceMatcher> logger;
        public override string Name => "全词语去符号顺序匹配";

        public override MatchResult Match(RoomContext supRoomContext, RoomContext otaRoomContext)
        {
            if (supRoomContext.RoomNameCleaned == otaRoomContext.RoomNameCleaned)
            {
                return MatchResult.Matched(Name, 1, 0) with { SupRoom = supRoomContext, OtaRoom = otaRoomContext }; ;
            }
            if (supRoomContext.RoomNameOriginal == otaRoomContext.RoomNameOriginal)
            {
                return MatchResult.Matched(Name, 1, 0) with { SupRoom = supRoomContext, OtaRoom = otaRoomContext }; ;
            }
            var supWords = FilterValidWords(supRoomContext.RoomNameWords);
            var otaWords = FilterValidWords(otaRoomContext.RoomNameWords);
            if (supWords.Count != otaWords.Count)
                return MatchResult.NotMatched(Name);


            for (int i = 0; i < supWords.Count; i++)
            {
                var sw = supWords[i];
                var ow = otaWords[i];

                // Id 必须相同（特殊情况再比较 WordText 可扩展）
                if (sw.WordAttr.Id != ow.WordAttr.Id)
                    return MatchResult.NotMatched(Name);
                // 如果有特殊词需要比较文本，也可添加类似条件：
                if (sw.WordAttr.Id == -1 && sw.WordAttr.WordText != ow.WordAttr.WordText)
                {
                    return MatchResult.NotMatched(Name);
                }
                // Numbers 必须一致（数量、顺序和值）
                if ((sw.Numbers?.Count ?? 0) != (ow.Numbers?.Count ?? 0))
                    return MatchResult.NotMatched(Name);
                if (sw.Numbers != null && ow.Numbers != null)
                {
                    for (int j = 0; j < sw.Numbers.Count; j++)
                    {
                        if (sw.Numbers[j] != ow.Numbers[j])
                            return MatchResult.NotMatched(Name);
                    }
                }
            }
            return MatchResult.Matched(Name, 2, 0) with { SupRoom = supRoomContext, OtaRoom = otaRoomContext };

        }


        private bool IsEqual(RoomContext supRoomContext, RoomContext otaRoomContext)
        {
            if (supRoomContext.RoomNameCleaned == otaRoomContext.RoomNameCleaned)
            {
                return true;
            }
            if (supRoomContext.RoomNameOriginal == otaRoomContext.RoomNameOriginal)
            {
                return true;
            }


            var supWords = FilterValidWords(supRoomContext.RoomNameWords);
            var otaWords = FilterValidWords(otaRoomContext.RoomNameWords);

            if (supWords.Count != otaWords.Count)
                return false;
            for (int i = 0; i < supWords.Count; i++)
            {
                var sw = supWords[i];
                var ow = otaWords[i];

                // Id 必须相同（特殊情况再比较 WordText 可扩展）
                if (sw.WordAttr.Id != ow.WordAttr.Id)
                    return false;

                // 如果有特殊词需要比较文本，也可添加类似条件：
                // if (sw.WordAttr.NeedCompareText && sw.WordAttr.WordText != ow.WordAttr.WordText) return false;

                // Numbers 必须一致（数量、顺序和值）
                if ((sw.Numbers?.Count ?? 0) != (ow.Numbers?.Count ?? 0))
                    return false;

                if (sw.Numbers != null && ow.Numbers != null)
                {
                    for (int j = 0; j < sw.Numbers.Count; j++)
                    {
                        if (sw.Numbers[j] != ow.Numbers[j])
                            return false;
                    }
                }
            }
            return true;
        }


        private List<RoomWord> FilterValidWords(List<RoomWord> words)
        {
            return words
                .Where(w => w.WordAttr.WordTypeId != 22 &&
                            (!w.WordAttr.IsExcludedInOtaRoom))  // true 表示可忽略 → 排除
                .ToList();
        }

    }
}

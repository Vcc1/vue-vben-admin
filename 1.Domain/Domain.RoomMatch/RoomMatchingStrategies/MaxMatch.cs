using Framework.Core.IOC;
using OTADomain.Matchings;
using RoomMatch.Core.RoomAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomMatch.Core.RoomMatchingStrategies
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class MaxMatch : BaseRoomMatcher
    {
        public override string Name => "主匹配算法";

        public override MatchResult Match(RoomContext sup, RoomContext ota)
        {
            if (sup.TypeWordDic.Count == 0) BaseRoomMatchHelper.BuildWordDictionary(sup);
            if (ota.TypeWordDic.Count == 0) BaseRoomMatchHelper.BuildWordDictionary(ota);

            if (sup.RoomNameCleaned == ota.RoomNameCleaned || sup.RoomNameOriginal == ota.RoomNameOriginal)
                return MatchResult.NotMatched(Name);

            // === 必须词匹配 ===
            var mustSup = sup.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new();
            var mustOta = ota.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new();

            var supStrict = mustSup.Where(w => w.WordAttr.WordTypeId == 2).Select(w => w.WordAttr.Id).ToList();
            var otaStrict = mustOta.Where(w => w.WordAttr.WordTypeId == 2).Select(w => w.WordAttr.Id).ToList();
            if (!WordCountEqualWithCompat(supStrict, otaStrict)) return MatchResult.NotMatched(Name);

            var supLoose = mustSup.Where(w => w.WordAttr.WordTypeId != 2).Select(w => w.WordAttr.Id).ToList();
            var otaLoose = mustOta.Where(w => w.WordAttr.WordTypeId != 2).Select(w => w.WordAttr.Id).ToList();
            if (!WordSetEqualWithCompat(supLoose, otaLoose)) return MatchResult.NotMatched(Name);

            int score = 0;

            // === 增益词：ota 缺失的 ===
            var boostSup = sup.TypeWordDic.GetValueOrDefault(WordCategory.Positive)?.Select(w => w.WordAttr).ToList() ?? new();
            var boostSupIds = boostSup.Select(static w => w.Id).ToHashSet();
            var boostOtaIds = ota.TypeWordDic.GetValueOrDefault(WordCategory.Positive)?.Select(w => w.WordAttr.Id).ToHashSet() ?? new();

            foreach (var otaPositiveId in boostOtaIds)
            {
                if (!IsCompatibleMatch(otaPositiveId, boostSupIds))
                    return MatchResult.NotMatched(Name);
            }

            foreach (var attr in boostSup)
            {
                if (!IsCompatibleMatch(attr.Id, boostOtaIds))
                    score += attr.Level ?? 1;
            }

            // === 减益词：ota 多出的 ===
            var negOta = ota.TypeWordDic.GetValueOrDefault(WordCategory.Negative)?.Select(w => w.WordAttr).ToList() ?? new();
            var negSupIds = sup.TypeWordDic.GetValueOrDefault(WordCategory.Negative)?.Select(w => w.WordAttr.Id).ToHashSet() ?? new();
            foreach (var attr in negOta)
            {
                if (!IsCompatibleMatch(attr.Id, negSupIds))
                    score += Math.Abs(attr.Level ?? 1);
            }

            // === 非必须词 差异 ===
            var noSup = sup.TypeWordDic.GetValueOrDefault(WordCategory.NoMustNeed)?.Select(w => w.WordAttr.Id).ToList() ?? new();
            var noOta = ota.TypeWordDic.GetValueOrDefault(WordCategory.NoMustNeed)?.Select(w => w.WordAttr.Id).ToList() ?? new();
            score += CountWithoutCompat(noSup, noOta);
            score += CountWithoutCompat(noOta, noSup);

            var status = score == 0 ? RoomMatchStatus.FullMatch : RoomMatchStatus.Match;
            return MatchResult.Matched(Name, (int)status, score);
        }

        private static bool WordCountEqualWithCompat(List<int> a, List<int> b)
        {
            var bList = new List<int>(b);
            foreach (var id in a)
            {
                var idx = bList.FindIndex(x => x == id || CompatibleMap.TryGetValue(id, out var comp) && comp.Contains(x));
                if (idx >= 0) bList.RemoveAt(idx);
                else return false;
            }
            return bList.Count == 0;
        }

        private static bool WordSetEqualWithCompat(List<int> a, List<int> b)
        {
            var setA = a.ToHashSet();
            var setB = b.ToHashSet();
            return setA.All(id => IsCompatibleMatch(id, setB)) &&
                   setB.All(id => IsCompatibleMatch(id, setA));
        }

        private static int CountWithoutCompat(List<int> a, List<int> b)
        {
            var bSet = b.ToHashSet();
            return a.Count(id => !IsCompatibleMatch(id, bSet));
        }

        private static bool IsCompatibleMatch(int id, HashSet<int> others)
        {
            if (others.Contains(id)) return true;
            if (CompatibleMap.TryGetValue(id, out var list))
                return list.Any(others.Contains);
            return false;
        }

        // 示例兼容词映射（可换成注入、缓存或服务）
        private static readonly Dictionary<int, List<int>> CompatibleMap = new()
        {
            //[1001] = new() { 2001, 2002 },
            //[1002] = new() { 2003 },
        };
    }

    public static class BaseRoomMatchHelper
    {
        public static void BuildWordDictionary(RoomContext ctx)
        {
            var dict = ctx.RoomNameWords.GroupBy(w =>
            {
                if (w.WordAttr.Id <= -2) return WordCategory.Unknown;
                if (w.WordAttr.IsMustNeed != true) return WordCategory.NoMustNeed;
                return w.WordAttr.EffectType switch
                {
                    EffectType.Positive => WordCategory.Positive,
                    EffectType.Negative => WordCategory.Negative,
                    _ => WordCategory.MustNeed
                };
            }).ToDictionary(g => g.Key, g => g.ToList());
            ctx.TypeWordDic = dict;
        }
    }
}

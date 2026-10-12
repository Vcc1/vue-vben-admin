using Framework.Core.IOC;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomMatch.Core.RoomMatchingStrategies
{
    /// <summary>
    /// 严格相等匹配器 - 必须词必须一致，不区分增益减益，每个必须词的词数量也必须一致，如果有数字数字必须一致
    /// </summary>
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class StrictEqualMatcher : BaseRoomMatcher
    {
        public override string Name => "严格相等必须词匹配";
        public override MatchResult Match(RoomContext sup, RoomContext ota)
        {
            // 构建词典如果尚未构建
            if (sup.TypeWordDic.Count == 0) BaseRoomMatchHelper.BuildWordDictionary(sup);
            if (ota.TypeWordDic.Count == 0) BaseRoomMatchHelper.BuildWordDictionary(ota);

            // 获取必须词（包括MustNeed、Positive、Negative）
            var supMustWords = GetMustWords(sup);
            var otaMustWords = GetMustWords(ota);

            // 检查必须词是否严格匹配
            if (!IsStrictWordMatch(supMustWords, otaMustWords))
            {
                return MatchResult.NotMatched(Name);
            }
            return MatchResult.Matched(Name, 1, 0) with { SupRoom = sup, OtaRoom = ota };
        }

        /// <summary>
        /// 获取所有必须词（包括MustNeed、Positive、Negative，不区分增益减益）
        /// </summary>
        private List<RoomWord> GetMustWords(RoomContext context)
        {
            var mustWords = new List<RoomWord>();

            // 添加必须词
            if (context.TypeWordDic.TryGetValue(WordCategory.MustNeed, out var mustNeedWords))
            {
                mustWords.AddRange(mustNeedWords);
            }

            // 添加增益词（作为必须词处理）
            if (context.TypeWordDic.TryGetValue(WordCategory.Positive, out var positiveWords))
            {
                mustWords.AddRange(positiveWords);
            }

            // 添加减益词（作为必须词处理）
            if (context.TypeWordDic.TryGetValue(WordCategory.Negative, out var negativeWords))
            {
                mustWords.AddRange(negativeWords);
            }

            return mustWords;
        }

        /// <summary>
        /// 严格匹配词语 - 每个词的ID、数量和数字都必须完全一致
        /// </summary>
        private bool IsStrictWordMatch(List<RoomWord> supWords, List<RoomWord> otaWords)
        {
            // 按词ID分组，并收集每个词ID对应的所有数字
            var supWordGroups = GetWordIdToNumbersMap(supWords);
            var otaWordGroups = GetWordIdToNumbersMap(otaWords);

            // 检查词ID集合是否相等（左边有的右边必须有，右边有的左边也必须有）
            if (supWordGroups.Count != otaWordGroups.Count)
            {
                return false;
            }

            // 检查每个词ID是否在两边都存在，且数字完全相等
            foreach (var supGroup in supWordGroups)
            {
                var wordId = supGroup.Key;
                var supNumbers = supGroup.Value;

                // 检查OTA是否有这个词ID
                if (!otaWordGroups.TryGetValue(wordId, out var otaNumbers))
                {
                    return false; // OTA中没有这个词ID
                }

                // 检查数字是否完全相等（数量和值都要相等）
                if (!AreNumberListsEqual(supNumbers, otaNumbers))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取词ID到数字列表的映射
        /// </summary>
        private Dictionary<int, List<decimal>> GetWordIdToNumbersMap(List<RoomWord> words)
        {
            var wordIdToNumbers = new Dictionary<int, List<decimal>>();

            foreach (var word in words)
            {
                var wordId = word.WordAttr.Id;

                // 如果词ID已存在，跳过后续的相同词ID（只取第一组数字）
                if (wordIdToNumbers.ContainsKey(wordId))
                {
                    continue;
                }

                // 添加第一次出现的词的数字
                var numbersList = new List<decimal>();
                if (word.Numbers != null && word.Numbers.Count > 0)
                {
                    numbersList.AddRange(word.Numbers);
                }

                wordIdToNumbers[wordId] = numbersList;
            }

            return wordIdToNumbers;
        }

        /// <summary>
        /// 检查两个数字列表是否完全相等（数量和值都相等）
        /// </summary>
        private bool AreNumberListsEqual(List<decimal> list1, List<decimal> list2)
        {
            if (list1.Count != list2.Count)
            {
                return false;
            }

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i] != list2[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}

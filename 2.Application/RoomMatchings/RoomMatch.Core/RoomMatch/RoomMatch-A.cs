using Framework.Core.IOC;
using Microsoft.Extensions.ObjectPool;
using Microsoft.IdentityModel.Tokens;
using Infrastructure.Sql.MengTuSup.Entitys;
using OTADomain.Matchings;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomDictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RoomMatch.Core.RoomMatch
{
    using OTARoomInfo = Infrastructure.Sql.RoomMapping.Entitys.OTARoomInfo;

    /// <summary>
    /// 适用于无符号匹配
    /// 匹配逻辑继承老朱的算法
    /// </summary>
    //[Service]
    public class RoomMatch_A : IRoomMatch
    {

        static DefaultObjectPool<RoomContext<OTARoomInfo>> mPool = new DefaultObjectPool<RoomContext<OTARoomInfo>>(new DefaultPooledObjectPolicy<RoomContext<OTARoomInfo>>());
        static DefaultObjectPool<RoomContext<SupRoomInfoView>> cPool = new DefaultObjectPool<RoomContext<SupRoomInfoView>>(new DefaultPooledObjectPolicy<RoomContext<SupRoomInfoView>>());
        public static List<(SupRoomInfoView CRoom, OTARoomInfo MRoom, RoomMatchStatus RoomMatchStatus, int Lavel)> MatchRoom(List<OTARoomInfo> mrooms, List<SupRoomInfoView> crooms, IRoomAnalyzer roomAnalysis)
        {
            var setting = new RoomAnalysisSetting() { WordDictSource = WordDictSource.Official };
            //var setting = new RoomAnalysisSetting() { WordDictSource = WordDictSource.Contrast_NoSymbol_Official };
            var mroomContexts = mrooms
                  .SelectMany(e =>
                  {
                      var arr = new RoomContext<OTARoomInfo>[2];
                      if (!string.IsNullOrEmpty(e.RoomNameEN))
                      {
                          arr[0] = mPool.Get().Reset(e.RoomNameEN, e, setting);
                      }
                      if (!string.IsNullOrEmpty(e.RoomNameCN) && e.RoomNameCN != e.RoomNameEN)
                      {
                          arr[1] = mPool.Get().Reset(e.RoomNameCN, e, setting);
                      }
                      return arr;
                  })

                  .Where(e => e != null)
                  .OrderBy(e => e.SourceData.Id)
                  .Select(e => roomAnalysis.Analysis(e))
                  .Where(e => e.RoomNameWords.Count() > 0)
                  .ToList();

            var list = crooms
                  //.Where(e => string.IsNullOrEmpty(e.CreateBy))
                  .Select(e =>
                  {
                      if (!string.IsNullOrEmpty(e.CreateBy))
                      {
                          var ota = mrooms.Where(m => m.Id == e.OTARoomId).FirstOrDefault();
                          return (e, ota, RoomMatchStatus.FullMatch, 0);
                      }
                      else if (!string.IsNullOrEmpty(e.SRoomNameEN))
                      {

                          var context = roomAnalysis.Analysis(cPool.Get().Reset(e.SRoomNameEN, e, setting));
                          var r = RoomMatch(mroomContexts, context);
                          if (r.RoomMatchStatus != RoomMatchStatus.NotMatch)
                          {
                              //已匹配
                              return (context.SourceData, r.RoomContext.SourceData, r.RoomMatchStatus, r.Lavel);
                          }
                          if (e.SRoomNameEN == e.SRoomNameCN)
                          {
                              ///返回不匹配
                              return (e, null, RoomMatchStatus.NotMatch, 0);
                          }
                          cPool.Return(context);
                      }

                      if (!string.IsNullOrEmpty(e.SRoomNameCN))
                      {
                          var context = roomAnalysis.Analysis(cPool.Get().Reset(e.SRoomNameCN, e, setting));
                          var r = RoomMatch(mroomContexts, context);
                          if (r.RoomMatchStatus != RoomMatchStatus.NotMatch)
                          {
                              //已匹配
                              return (context.SourceData, r.RoomContext.SourceData, r.RoomMatchStatus, r.Lavel);
                          }
                          cPool.Return(context);
                      }
                      return (e, null, RoomMatchStatus.NotMatch, 0);
                  }).ToList();
            foreach (var item in mroomContexts)
            {
                mPool.Return(item);
            }
            return list;
        }



        public static bool MatchRoom(string mRoomName, string cRoomName, IRoomAnalyzer roomAnalysis)
        {
            if (string.IsNullOrWhiteSpace(mRoomName) || string.IsNullOrWhiteSpace(cRoomName) || roomAnalysis == null)
            {
                return false;
            }
            var setting = new RoomAnalysisSetting() { WordDictSource = WordDictSource.Official };
            var mContext = roomAnalysis.Analysis(new RoomContext(mRoomName, setting));
            var cContext = roomAnalysis.Analysis(new RoomContext(cRoomName, setting));
            var (status, _) = RoomMatch2(mContext, cContext);

            return status != RoomMatchStatus.NotMatch;
        }



        private static (RoomContext<MT> RoomContext, RoomMatchStatus RoomMatchStatus, int Lavel) RoomMatch<MT, CT>(List<RoomContext<MT>> mRoomContextList, RoomContext<CT> cRoomContext)
        {
            RoomContext<MT> bestMatch = null;
            RoomMatchStatus bestStatus = RoomMatchStatus.NotMatch;
            int bestDistance = int.MaxValue;
            mRoomContextList.ForEach(roomContext =>
            {
                构建字典2(roomContext);
            });
            构建字典2(cRoomContext);

            foreach (var mRoom in mRoomContextList)
            {
                var (status, distance) = RoomMatch2(mRoom, cRoomContext);

                if (status == RoomMatchStatus.TextMatch)
                {
                    // 优先返回第一个全量匹配
                    return (mRoom, RoomMatchStatus.TextMatch, 0);
                }
                if (status == RoomMatchStatus.NotMatch)
                {
                    continue;
                }

                if (status > bestStatus || (status == RoomMatchStatus.Match && bestDistance > distance))
                {
                    bestMatch = mRoom;
                    bestStatus = status;
                    bestDistance = distance;
                }
            }

            if (bestMatch != null)
            {
                return (bestMatch, bestStatus, bestDistance);
            }

            return (null, RoomMatchStatus.NotMatch, int.MaxValue);
        }

        private static void 构建字典<CT>(RoomContext<CT> cRoomContext)
        {
            cRoomContext.TypeWordDic = cRoomContext.RoomNameWords.Where(word => word.WordAttr.Id > -2).GroupBy(e =>
            {
                if (e.WordAttr.Id == -1)
                {
                    return WordCategory.Unknown;
                }
                else if (e.WordAttr.IsMustNeed != true)
                {
                    return WordCategory.NoMustNeed;
                }
                else if (e.WordAttr.EffectType == EffectType.Default)
                {
                    if (ShouldIgnoreWord(cRoomContext, e.WordAttr.Id))
                    {
                        return WordCategory.NoMustNeed;
                    }
                    else
                    {
                        return WordCategory.MustNeed;
                    }
                }
                else if (e.WordAttr.EffectType == EffectType.Positive)
                {
                    return WordCategory.Positive;
                }
                else if (e.WordAttr.EffectType == EffectType.Negative)
                {
                    return WordCategory.Negative;
                }
                return WordCategory.Unknown;
            }).ToDictionary(e => e.Key, e => e.ToList());
        }
        private static void 构建字典2(RoomContext cRoomContext)
        {
            var dict = new Dictionary<WordCategory, List<RoomWord>>();
            bool seen2790 = false;

            foreach (var word in cRoomContext.RoomNameWords)
            {
                if (word.WordAttr.Id <= -2)
                    continue;

                WordCategory category;
                if (word.IsExclude)
                {
                    category = WordCategory.NoMustNeed;
                    //continue;
                }
                else if (word.WordAttr.Id == -1)
                {
                    category = WordCategory.Unknown;
                }
                else if (word.WordAttr.IsMustNeed != true)
                {
                    category = WordCategory.NoMustNeed;
                }
                else if (word.WordAttr.EffectType == EffectType.Default)
                {
                    category = WordCategory.MustNeed;
                }
                else if (word.WordAttr.EffectType == EffectType.Positive)
                {
                    category = WordCategory.Positive;
                }
                else if (word.WordAttr.EffectType == EffectType.Negative)
                {
                    category = WordCategory.Negative;
                }
                else
                {
                    category = WordCategory.Unknown;
                }

                if (!dict.TryGetValue(category, out var list))
                {
                    list = new List<RoomWord>();
                    dict[category] = list;
                }

                list.Add(word);
            }

            // === 判断并补充 MustNeed 类别逻辑 ===
            if (!dict.TryGetValue(WordCategory.MustNeed, out var mustNeedList) || mustNeedList.Count == 0)
            {
                mustNeedList = new List<RoomWord>();

                // 加入 Positive 类别词
                if (dict.TryGetValue(WordCategory.Positive, out var positiveList))
                    mustNeedList.AddRange(positiveList);

                // 加入 Negative 类别词
                if (dict.TryGetValue(WordCategory.Negative, out var negativeList))
                    mustNeedList.AddRange(negativeList);

                // 如果还是为空，则用 NoMustNeed 类别词填充   但不填充重复词组
                if (mustNeedList.Count == 0 && dict.TryGetValue(WordCategory.NoMustNeed, out var noMustNeedList))
                    mustNeedList.AddRange(noMustNeedList.Where(e => !e.IsExclude).ToList());

                dict[WordCategory.MustNeed] = mustNeedList;
            }


            cRoomContext.TypeWordDic = dict;
        }

        /// <summary>
        /// 母房型 子房型 匹配 数量可以不一致
        /// </summary>
        /// <param name="mRoomInfo">母房型</param>
        /// <param name="cRoomInfo">子房型</param>
        /// <returns>(匹配状态,边界距离)</returns>
        //public static (RoomMatchStatus, int) RoomMatch(RoomContext mRoomInfo, RoomContext cRoomInfo)
        //{
        //    if (string.IsNullOrEmpty(mRoomInfo.RoomNameOriginal) || string.IsNullOrEmpty(mRoomInfo.RoomNameOriginal))
        //    {
        //        return (RoomMatchStatus.NotMatch, 0);
        //    }

        //    if (mRoomInfo.RoomNameOriginal == mRoomInfo.RoomNameOriginal)
        //    {
        //        return (RoomMatchStatus.TextMatch, 0);
        //    }


        //    if (string.IsNullOrEmpty(mRoomInfo.RoomNameCleaned) || string.IsNullOrEmpty(mRoomInfo.RoomNameCleaned))
        //    {
        //        return (RoomMatchStatus.NotMatch, 0);
        //    }

        //    if (mRoomInfo.RoomNameCleaned == cRoomInfo.RoomNameCleaned)
        //    {
        //        return (RoomMatchStatus.TextMatch, 0);
        //    }



        //    if (mRoomInfo.TypeWordDic.Count == 0)
        //    {
        //        构建字典2(mRoomInfo);
        //    }
        //    if (cRoomInfo.TypeWordDic.Count == 0)
        //    {
        //        构建字典2(cRoomInfo);
        //    }

        //    if (mRoomInfo.TypeWordDic.Count == 0)
        //    {
        //        return (RoomMatchStatus.NotMatch, 0);
        //    }
        //    if (cRoomInfo.TypeWordDic.Count == 0)
        //    {
        //        return (RoomMatchStatus.NotMatch, 0);
        //    }



        //    var noneWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Unknown) ?? new List<RoomWord>();
        //    var noneMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Unknown) ?? new List<RoomWord>();



        //    var noneTexts = noneWord.Select(e => e.WordText).ToHashSet();
        //    var noneMTexts = noneMWord.Select(e => e.WordText).ToHashSet();
        //    if (!noneTexts.SetEquals(noneMTexts))
        //    {
        //        // 集合不相同
        //        return (RoomMatchStatus.NotMatch, 0);
        //    }

        //    var mustNeedWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new List<RoomWord>();
        //    var mustNeedMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new List<RoomWord>();



        //    var mustNeedWordId = mustNeedWord.Select(e => e.WordAttr.Id).ToHashSet();
        //    var mustNeedMWordId = mustNeedMWord.Select(e => e.WordAttr.Id).ToHashSet();
        //    if (!mustNeedWordId.SetEquals(mustNeedMWordId))
        //    {
        //        // 集合不相同
        //        return (RoomMatchStatus.NotMatch, 0);
        //    }


        //    int boundaryDistance = 0;

        //    // === 增益词未匹配：累加 Level 值（正数）===
        //    var positiveWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Positive) ?? new List<RoomWord>();
        //    var positiveMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Positive) ?? new List<RoomWord>();
        //    var boostItem = positiveWord
        //        .GroupBy(e => e.WordAttr.Id)
        //        .ToDictionary(g => g.Key, g => g.First().WordAttr.Level ?? 1); // 默认Level=1
        //    var boostM = positiveMWord
        //        .Select(e => e.WordAttr.Id)
        //        .ToHashSet();

        //    foreach (var kv in boostItem)
        //    {
        //        if (!boostM.Contains(kv.Key))
        //            boundaryDistance += kv.Value;
        //    }

        //    // === 减益词多出：累加 Level 绝对值（负数）===
        //    var negativeWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Negative) ?? new List<RoomWord>();
        //    var negativeMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Negative) ?? new List<RoomWord>();
        //    var penaltyItem = negativeWord
        //        .Select(e => e.WordAttr.Id)
        //        .ToHashSet();
        //    var penaltyM = negativeMWord
        //        .GroupBy(e => e.WordAttr.Id)
        //        .ToDictionary(g => g.Key, g => Math.Abs(g.First().WordAttr.Level ?? -1)); // 默认Level=-1

        //    foreach (var kv in penaltyM)
        //    {
        //        if (!penaltyItem.Contains(kv.Key))
        //            boundaryDistance += kv.Value;
        //    }


        //    // === 非必须词（可选）不匹配 ===
        //    var noRequiredWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.NoMustNeed) ?? new List<RoomWord>();
        //    var noRequiredMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.NoMustNeed) ?? new List<RoomWord>();
        //    var cNoRequired = noRequiredWord.Select(e => e.WordAttr.Id).ToHashSet();
        //    var mNoRequired = noRequiredMWord.Select(e => e.WordAttr.Id).ToHashSet();
        //    boundaryDistance += cNoRequired.Except(mNoRequired).Count();

        //    var status = boundaryDistance == 0 ? RoomMatchStatus.FullMatch : RoomMatchStatus.Match;
        //    return (status, boundaryDistance);
        //}

        /// <summary>
        /// 数量必须一致
        /// </summary>
        /// <param name="mRoomInfo"></param>
        /// <param name="cRoomInfo"></param>
        /// <returns></returns>
        public static (RoomMatchStatus, int) RoomMatch2(RoomContext mRoomInfo, RoomContext cRoomInfo)
        {
            if (mRoomInfo.TypeWordDic.Count == 0)
            {
                构建字典2(mRoomInfo);
            }
            if (cRoomInfo.TypeWordDic.Count == 0)
            {
                构建字典2(cRoomInfo);
            }

            if (mRoomInfo.TypeWordDic.Count == 0)
            {
                return (RoomMatchStatus.NotMatch, 0);
            }
            if (cRoomInfo.TypeWordDic.Count == 0)
            {
                return (RoomMatchStatus.NotMatch, 0);
            }



            if (mRoomInfo.RoomNameCleaned == cRoomInfo.RoomNameCleaned || mRoomInfo.RoomNameOriginal == cRoomInfo.RoomNameOriginal)
            {
                return (RoomMatchStatus.TextMatch, 0);
            }

            var noneWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Unknown) ?? new List<RoomWord>();
            var noneMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Unknown) ?? new List<RoomWord>();


            var noneTexts = noneWord.Select(e => e.WordAttr.WordText);
            var noneMTexts = noneMWord.Select(e => e.WordAttr.WordText);

            int boundaryDistance = 0;
            //if (!WordCountEqual(noneTexts, noneMTexts))
            //    return (RoomMatchStatus.NotMatch, 0);
            if (!WordCountGrowOnlyNoNewKeysFast(noneMTexts, noneTexts))
            {
                return (RoomMatchStatus.NotMatch, 0);
            }
            boundaryDistance += (noneTexts.Count() - noneMTexts.Count());

            //var mustNeedWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new List<RoomWord>();
            //var mustNeedMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new List<RoomWord>();
            //var mustNeedWordId = mustNeedWord.Select(e => e.WordAttr.Id);
            //var mustNeedMWordId = mustNeedMWord.Select(e => e.WordAttr.Id);

            //if (!WordCountEqual(mustNeedWordId, mustNeedMWordId))
            //    return (RoomMatchStatus.NotMatch, 0);

            var mustNeedWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new List<RoomWord>();
            var mustNeedMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.MustNeed) ?? new List<RoomWord>();

            // 1. 需要数量严格一致的（TypeId==1/3/5）
            // 1. 需要数量严格一致的（WordTypeId == 2）
            var strictWordIds = mustNeedWord.Where(e => e.WordAttr.WordTypeId == 2).Select(e => e.WordAttr.Id).ToList();
            var strictMWordIds = mustNeedMWord.Where(e => e.WordAttr.WordTypeId == 2).Select(e => e.WordAttr.Id).ToList();

            // 严格词类型，比较数量 + 内容（通过频次）
            if (!WordCountGrowOnlyNoNewKeysFast(strictMWordIds, strictWordIds))
            {
                return (RoomMatchStatus.NotMatch, 0);
            }
            boundaryDistance += (strictWordIds.Count() - strictMWordIds.Count());
            boundaryDistance *= 5;

            //if (!WordCountEqual(strictWordIds, strictMWordIds))
            //{
            //    return (RoomMatchStatus.NotMatch, 0);
            //}

            // 2. 只需内容一致的（其它类型）
            var looseWordIds = mustNeedWord.Where(e => e.WordAttr.WordTypeId != 2).Select(e => e.WordAttr.Id).ToList();
            var looseMWordIds = mustNeedMWord.Where(e => e.WordAttr.WordTypeId != 2).Select(e => e.WordAttr.Id).ToList();

            if (!looseWordIds.ToHashSet().SetEquals(looseMWordIds.ToHashSet()))
            {
                return (RoomMatchStatus.NotMatch, 0);
            }

            // 宽松词类型，比较内容（忽略频次）

            // 差异词数量：取对称差集数量
            var symmetricDiff = looseWordIds.Except(looseMWordIds).Count() + looseMWordIds.Except(looseWordIds).Count();
            boundaryDistance += symmetricDiff;

            // === 增益词：母房型有，子房型必须也有 ===
            var positiveMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Positive) ?? new List<RoomWord>();
            var positiveWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Positive) ?? new List<RoomWord>();

            var boostItem = positiveMWord
                .Select(e => e.WordAttr.Id)
                .ToHashSet(); // 母房型的正面词 Id

            var boost = positiveWord
                .Select(e => e.WordAttr.Id)
                .ToHashSet(); // 子房型的正面词 Id
            boundaryDistance += boost.Count(id => !boostItem.Contains(id));
            foreach (var id in boostItem)
            {
                if (!boost.Contains(id))
                {
                    return (RoomMatchStatus.NotMatch, 0);
                }
            }

            // === 减益词：子房型有，母房型必须也要有 ===
            var negativeWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Negative) ?? new List<RoomWord>();
            var negativeMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.Negative) ?? new List<RoomWord>();

            var penaltyItem = negativeWord
                .Select(e => e.WordAttr.Id)
                .ToList(); // 子房型的负面词 Id

            var penaltyM = negativeMWord
                .Select(e => e.WordAttr.Id)
                .ToList(); // 母房型的负面词 Id
            boundaryDistance += penaltyM.Count(id => !penaltyItem.Contains(id));
            foreach (var id in penaltyItem)
            {
                if (!penaltyM.Contains(id))
                {
                    return (RoomMatchStatus.NotMatch, 0);
                }
            }
            ///上述权重*2;
            boundaryDistance *= 2;

            // === 非必须词（可选）不匹配 ===
            var noRequiredWord = cRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.NoMustNeed) ?? new List<RoomWord>();
            var noRequiredMWord = mRoomInfo.TypeWordDic.GetValueOrDefault(WordCategory.NoMustNeed) ?? new List<RoomWord>();

            var cNoRequired = noRequiredWord.Select(e => e.WordAttr.Id).ToList();
            var mNoRequired = noRequiredMWord.Select(e => e.WordAttr.Id).ToList();

            // 计算差异数量（A有B没有，B有A没有）
            boundaryDistance += cNoRequired.Except(mNoRequired).Count() + mNoRequired.Except(cNoRequired).Count();



            var status = boundaryDistance == 0 ? RoomMatchStatus.FullMatch : RoomMatchStatus.Match;

            if (mRoomInfo.RoomNameWords.Any(e => e.WordAttr.Id == 3294) && status == RoomMatchStatus.Match)
            {
                return (RoomMatchStatus.NotMatch, 0);
            }

            return (status, boundaryDistance);
        }

        private static bool WordCountEqual<T>(IEnumerable<T> a, IEnumerable<T> b)
        {
            var countMap = new Dictionary<T, int>();

            foreach (var item in a)
            {
                if (countMap.TryGetValue(item, out var cnt))
                    countMap[item] = cnt + 1;
                else
                    countMap[item] = 1;
            }

            foreach (var item in b)
            {
                if (!countMap.TryGetValue(item, out var cnt)) return false;

                if (cnt == 1)
                    countMap.Remove(item);
                else
                    countMap[item] = cnt - 1;
            }

            return countMap.Count == 0;
        }
        public static bool WordCountGrowOnlyNoNewKeysFast<T>(
            IEnumerable<T> a,
            IEnumerable<T> b,
            IEqualityComparer<T> comparer = null)
        {
            // 如果你这里能保证非空，可以去掉这些检查，省一点点开销
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (b is null) throw new ArgumentNullException(nameof(b));

            // 预估容量：减少扩容（热路径很关键）
            int capacity = 0;
            if (a is ICollection<T> ac) capacity = ac.Count;
            else if (a is IReadOnlyCollection<T> arc) capacity = arc.Count;

            var map = capacity > 0
                ? new Dictionary<T, int>(capacity, comparer)
                : new Dictionary<T, int>(comparer);

            // 统计 A：map[key] = 需要的数量（下限）
            foreach (var x in a)
            {
                if (map.TryGetValue(x, out var cnt))
                    map[x] = cnt + 1;
                else
                    map[x] = 1;
            }

            // 遍历 B：只能用 A 里已有 key；每遇到一次就 -1（代表“覆盖掉一个需求”）
            foreach (var x in b)
            {
                if (!map.TryGetValue(x, out var cnt))
                    return false; // B 引入新元素

                map[x] = cnt - 1;
            }

            // 最终：所有值都必须 <= 0（表示 B 覆盖数量 >= A 需求）
            foreach (var cnt in map.Values)
            {
                if (cnt > 0) return false; // B 还不够
            }

            return true;
        }

        private static bool ShouldIgnoreWord(RoomContext context, int wordId)
        {
            return wordId == 2790 &&
                   context.RoomNameWords.Any(x =>
                       x.WordAttr.Id != 2790 &&
                       (x.WordAttr.WordTypeId == 1 || x.WordAttr.WordTypeId == 3));
        }


    }
}

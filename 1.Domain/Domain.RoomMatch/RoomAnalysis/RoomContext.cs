using RoomMatch.Core.RoomDictionary;

namespace RoomMatch.Core.RoomAnalysis
{
    public class RoomContext
    {
        public RoomAnalysisSetting Setting { get; set; }
        /// <summary>
        /// 原始房型名称
        /// </summary>
        public string RoomNameOriginal { get; internal set; }
        /// <summary>
        /// 格式清洁后的房型名称
        /// </summary>
        public string RoomNameCleaned { get; set; }
        /// <summary>
        /// 分词后的词列表(按词顺序提供)
        /// </summary>
        public List<RoomWord> RoomNameWords { get; set; } = new List<RoomWord>();
        public void Reset()
        {
            RoomNameWords.Clear();
            RoomNameOriginal = string.Empty;
            RoomNameCleaned = string.Empty;
            TypeWordDic = null;
            Setting = null;
        }

        public Dictionary<WordCategory, List<RoomWord>> TypeWordDic { get; set; } = new Dictionary<WordCategory, List<RoomWord>>();
        public string ErrorMsg { get; set; }
        /// <summary>
        /// 存在忽略词
        /// </summary>
        public bool IsExcludedInOtaRoom { get; set; }
        /// <summary>
        /// 存在未知字符不建议建房型
        /// </summary>
        public bool IsUnknownChar { get; set; } = false;

        public RoomContext(string RoomName, RoomAnalysisSetting setting)
        {
            RoomNameOriginal = RoomName;
            Setting = setting;
        }

        public RoomContext(string RoomName)
        {
            RoomNameOriginal = RoomName;
        }

        public RoomContext()
        {
        }

        public void PrintDebugInfo()
        {
            Console.WriteLine("======== RoomContext 调试信息 ========");
            Console.WriteLine($"原始房型名称 (RoomNameOriginal): {RoomNameOriginal}");
            Console.WriteLine($"清洁后名称   (RoomNameCleaned) : {RoomNameCleaned}");
            Console.WriteLine("分词结果 (RoomNameWords):");

            if (RoomNameWords == null || RoomNameWords.Count == 0)
            {
                Console.WriteLine("  [无分词结果]");
            }
            else
            {
                for (int i = 0; i < RoomNameWords.Count; i++)
                {
                    var word = RoomNameWords[i];
                    Console.WriteLine($"  [{i}] Text: {word.WordText}, Id: {word.WordAttr?.Id}, Type: {word.WordAttr?.WordTypeName}, MustNeed: {word.WordAttr?.IsMustNeed}, EffectType: {word.WordAttr?.EffectType}, Numbers: {string.Join(',', word.Numbers)}");
                }
            }

            Console.WriteLine("=======================================");
        }
    }

    public enum WordCategory
    {
        /// <summary>
        /// 未知词
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// 非必须词
        /// </summary>
        NoMustNeed = 0,

        /// <summary>
        /// 必须词
        /// </summary>
        MustNeed = 1,

        /// <summary>
        /// 增益词
        /// </summary>
        Positive = 2,

        /// <summary>
        /// 减益词
        /// </summary>
        Negative = 3
    }

    public class RoomContext<T> : RoomContext
    {
        public RoomContext(string RoomName, T sourceData, RoomAnalysisSetting setting) : base(RoomName, setting)
        {
            SourceData = sourceData;
        }

        public RoomContext(string RoomName, T sourceData) : base(RoomName)
        {
            SourceData = sourceData;
        }

        public RoomContext()
        {
        }

        public RoomContext<T> Reset(string basicRoomNameEn, T e)
        {
            base.Reset();
            RoomNameOriginal = basicRoomNameEn;
            SourceData = e;
            return this;
        }

        public RoomContext<T> Reset(string basicRoomNameEn, T e, RoomAnalysisSetting setting)
        {
            base.Reset();
            RoomNameOriginal = basicRoomNameEn;
            SourceData = e;
            Setting = setting;
            return this;
        }

        public T SourceData { get; private set; }
    }
}

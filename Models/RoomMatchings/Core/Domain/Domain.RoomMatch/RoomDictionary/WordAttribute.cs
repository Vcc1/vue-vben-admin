using OTADomain.Matchings;
using System.ComponentModel;

namespace RoomMatch.Core.RoomDictionary
{
    public class WordAttribute
    {
        public int Id { get; set; }
        public int WordTypeId { get; set; }
        public string WordTypeName { get; set; }
        public string WordName { get; set; }
        public string WordText { get; set; }
        public int State { get; set; }
        public bool IsMustNeed { get; set; }
        public EffectType EffectType { get; set; }
        /// <summary>
        /// -10 到 10 负分为减益  加分为增益(暂时不用)
        /// </summary>
        public int? Level { get; set; }
        public bool IsExcludedInOtaRoom { get; set; }

        public static WordAttribute SpaceWordAttribute { get; set; } = new WordAttribute
        {
            Id = -100,
            WordTypeId = 22,
            WordTypeName = "符号",
            Level = 0,
            WordName = "空格",
            WordText = " ",
            IsMustNeed = false,
            EffectType = EffectType.Default
        };
    }

    /// <summary>
    /// 分词类型枚举
    /// </summary>
    [Description("分词类型")]
    public enum SegmentTypeEnum
    {
        /// <summary>
        /// 正式名
        /// </summary>
        [Description("正式名"), Theme("warning")]
        Name = 1,

        /// <summary>
        /// 英文名
        /// </summary>
        [Description("英文名"), Theme("info")]
        EnName = 2,

        /// <summary>
        /// 同义词
        /// </summary>
        [Description("同义词"), Theme("danger")]
        Synonyms = 3,

        /// <summary>
        /// 别名
        /// </summary>
        [Description("别名"), Theme("success")]
        AnotherName = 4
    }
}

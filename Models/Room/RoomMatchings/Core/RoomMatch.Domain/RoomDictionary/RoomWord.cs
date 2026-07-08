using static NReco.Text.AhoCorasickDoubleArrayTrie<RoomMatch.Core.RoomDictionary.WordAttribute>;

namespace RoomMatch.Core.RoomDictionary
{
    public record RoomWord
    {
        public RoomWord(WordAttribute wordAttr)
        {
            IsMustNeed = wordAttr.IsMustNeed;
            WordAttr = wordAttr;
        }

        /// <summary>
        /// 词原始信息
        /// </summary>
        public WordAttribute WordAttr { get; private set; }

        /// <summary>
        /// 词在房型中的位置
        /// </summary>
        public int Begin { get; set; }

        /// <summary>
        /// 词在房型中的位置
        /// </summary>
        public int End { get; set; }

        public string WordText => WordAttr.WordText;

        public ReadOnlySpan<char> SourceText(string roomName)
        {
            return roomName.AsSpan(Begin, End - Begin);
        }

        public bool IsExclude { get; set; }
        public bool IsMustNeed { get; set; }
        public bool IsHit { get; set; }

        /// <summary>
        /// 各项细分词
        /// </summary>
        public List<Hit> Hits { get; set; }

        public List<decimal> Numbers { get; set; } = new List<decimal>();
    }
}

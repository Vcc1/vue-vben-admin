namespace RoomMatch.Core.RoomAnalysis
{
    public class RoomAnalysisSetting
    {
        /// <summary>
        /// 数字是否转换为，号匹配处理 用于模糊匹配  词库暂未完全兼容临时做开关兼容新老词库
        /// </summary>
        public bool IsNumberEscape { get; set; } = false;

        public CleanFormatType CleanFormatType { get; set; } = CleanFormatType.STRONG_NORMALIZE;
        /// <summary>
        /// 未知词是否按空格拆分
        /// </summary>
        public bool IsUnknownWordSplitBySpace { get; set; } = true;
        public WordDictSource WordDictSource { get; set; } = WordDictSource.Official;
    }
    public enum WordDictSource
    {
        Official = 0,
        Contrast_Official = 1
    }
    public enum CleanFormatType
    {
        /// <summary>
        /// 转小写 + 特殊符号转常见符号 + 合并空格
        /// </summary>
        BASIC_NORMALIZE = 1,
        /// <summary>
        /// 转小写 + 所有符号转空格(/-除外) + 合并空格
        /// </summary>
        STRONG_NORMALIZE = 2
    }
}

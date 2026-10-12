using Framework.Core.IOC;

namespace RoomMatch.Core.RoomAnalysis
{
    /// <summary>
    /// 解析器
    /// 
    /// 解析步骤
    /// 1 预处理 全角字符转半角字符 部分字符转,
    /// 2 不支持语言过滤
    /// 3 AC分词
    /// 4 分词后处理 (语义处理)
    /// 5 属性化处理
    /// </summary>
    [Service(typeof(RoomAnalyzer))]
    public interface IRoomAnalyzer
    {
        RoomContext Analysis(RoomContext room);

        RoomContext<T> Analysis<T>(RoomContext<T> room);
    }
}

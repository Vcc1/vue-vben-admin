using System.Threading.Channels;

namespace QueryPriceSupRoomBuilder;

/// <summary>
/// 单轮房型构建使用的内存管道。
/// 任务队列连接读取步骤和查价步骤，结果队列连接查价步骤和持久化步骤；
/// 每一轮都会重新创建，避免跨轮残留。
/// </summary>
public sealed class RoomBuildTaskPipeline
{
    readonly Channel<IReadOnlyList<QueryPriceSupRoomBuildTaskModel>> _taskQueue;
    readonly Channel<RoomBuildTaskProcessingItem> _persistenceQueue;

    public RoomBuildTaskPipeline()
    {
        _taskQueue = Channel.CreateUnbounded<IReadOnlyList<QueryPriceSupRoomBuildTaskModel>>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });

        _persistenceQueue = Channel.CreateUnbounded<RoomBuildTaskProcessingItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// 读取步骤写入本轮待查价任务组。
    /// </summary>
    public ChannelWriter<IReadOnlyList<QueryPriceSupRoomBuildTaskModel>> TaskWriter => _taskQueue.Writer;

    /// <summary>
    /// 查价步骤读取本轮待查价任务组。
    /// </summary>
    public ChannelReader<IReadOnlyList<QueryPriceSupRoomBuildTaskModel>> TaskReader => _taskQueue.Reader;

    /// <summary>
    /// 查价步骤写入处理结果，供持久化步骤消费。
    /// </summary>
    public ChannelWriter<RoomBuildTaskProcessingItem> PersistenceWriter => _persistenceQueue.Writer;

    /// <summary>
    /// 持久化步骤读取处理结果。
    /// </summary>
    public ChannelReader<RoomBuildTaskProcessingItem> PersistenceReader => _persistenceQueue.Reader;
}

/// <summary>
/// 查价步骤输出给持久化步骤的单任务处理结果。
/// 成功时携带 ProcessTaskResult，异常时携带 Error，由持久化步骤统一落状态。
/// </summary>
public sealed record RoomBuildTaskProcessingItem(
    QueryPriceSupRoomBuildTaskModel Task,
    ProcessTaskResult? Result,
    Exception? Error)
{
    public static RoomBuildTaskProcessingItem Success(QueryPriceSupRoomBuildTaskModel task, ProcessTaskResult result)
    {
        return new RoomBuildTaskProcessingItem(task, result, null);
    }

    public static RoomBuildTaskProcessingItem Failure(QueryPriceSupRoomBuildTaskModel task, Exception error)
    {
        return new RoomBuildTaskProcessingItem(task, null, error);
    }
}

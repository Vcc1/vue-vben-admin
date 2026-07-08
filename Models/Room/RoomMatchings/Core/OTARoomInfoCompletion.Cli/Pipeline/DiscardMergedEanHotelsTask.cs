using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public sealed class DiscardMergedEanHotelsTask
{
    private const string InputQueueName = "merged_ean_hotels";

    private readonly ILogger<DiscardMergedEanHotelsTask> _logger;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public DiscardMergedEanHotelsTask(ILogger<DiscardMergedEanHotelsTask> logger, OtaRoomInfoCompletionMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<OtaRoomInfoCompletionWorkItem> mergedEanHotelReader, CancellationToken cancellationToken)
    {
        var discardedCount = 0;

        await foreach (var _ in mergedEanHotelReader.ReadAllAsync(cancellationToken))
        {
            _metrics.DecreaseQueueDepth(InputQueueName);
            discardedCount++;
        }

        _logger.LogInformation("测试模式已消费合并后的酒店数据, Count: {Count}", discardedCount);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QueryPriceSupRoomBuilder;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace Infrastructure.Redis.TriggerQueue;

public sealed class TriggerQueueRedisContext : RedisContextBase, ITriggerQueueClient
{
    readonly TriggerQueueMetrics _metrics;
    readonly ILogger<TriggerQueueRedisContext> _logger;

    public string RoomBuildHotelQueueKey { get; }

    public TriggerQueueRedisContext(
        IConfiguration configuration,
        TriggerQueueMetrics metrics,
        ILogger<TriggerQueueRedisContext> logger)
        : base(configuration, "BaseDataTriggerQueue")
    {
        _metrics = metrics;
        _logger = logger;
        RoomBuildHotelQueueKey = configuration["TriggerQueue:RoomBuildHotelKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(RoomBuildHotelQueueKey))
        {
            RoomBuildHotelQueueKey = TriggerQueueKeys.RoomBuildHotel;
        }
    }

    public async Task EnqueueAsync(string queueKey, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DB.ListLeftPushAsync(queueKey, value);
        _metrics.Record(queueKey, "enqueue", "success", 1);
    }

    public async Task<IReadOnlyList<string>> DequeueAsync(string queueKey, int batchSize, CancellationToken cancellationToken)
    {
        var values = new List<string>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RedisValue value;
            try
            {
                value = await DB.ListRightPopAsync(queueKey);
            }
            catch (Exception ex)
            {
                _metrics.Record(queueKey, "dequeue", "error", 1);
                _logger.LogError(ex, "Failed to dequeue trigger queue. Queue={Queue}", queueKey);
                throw;
            }

            if (value.IsNullOrEmpty)
            {
                break;
            }

            values.Add(value.ToString());
        }

        _metrics.Record(queueKey, "dequeue", "success", values.Count);
        return values;
    }

    public Task EnqueueRoomBuildHotelAsync(string mtHotelCode, CancellationToken cancellationToken)
    {
        return EnqueueAsync(RoomBuildHotelQueueKey, mtHotelCode, cancellationToken);
    }

    public Task<IReadOnlyList<string>> DequeueRoomBuildHotelsAsync(int batchSize, CancellationToken cancellationToken)
    {
        return DequeueAsync(RoomBuildHotelQueueKey, batchSize, cancellationToken);
    }
}

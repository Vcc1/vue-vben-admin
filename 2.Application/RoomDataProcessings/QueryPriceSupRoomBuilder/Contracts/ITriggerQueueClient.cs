namespace QueryPriceSupRoomBuilder;

public interface ITriggerQueueClient
{
    Task EnqueueAsync(string queueKey, string value, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> DequeueAsync(string queueKey, int batchSize, CancellationToken cancellationToken);

    Task EnqueueRoomBuildHotelAsync(string mtHotelCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> DequeueRoomBuildHotelsAsync(int batchSize, CancellationToken cancellationToken);
}

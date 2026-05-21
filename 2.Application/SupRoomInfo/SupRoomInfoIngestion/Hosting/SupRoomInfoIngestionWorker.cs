namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoIngestionWorker : BackgroundService
{
    readonly SupRoomInfoProduceStep _produceStep;
    readonly SupRoomInfoConsumeStep _consumeStep;
    readonly SupRoomInfoIngestionQueue _queue;
    readonly ILogger<SupRoomInfoIngestionWorker> _logger;

    public SupRoomInfoIngestionWorker(
        SupRoomInfoProduceStep produceStep,
        SupRoomInfoConsumeStep consumeStep,
        SupRoomInfoIngestionQueue queue,
        ILogger<SupRoomInfoIngestionWorker> logger)
    {
        _produceStep = produceStep;
        _consumeStep = consumeStep;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ThreadPool.SetMinThreads(16, 16);
        _logger.LogInformation("SupRoomInfo 入库开始。CPU={CpuCount}", Environment.ProcessorCount);
        var produceTask = _produceStep.RunAsync(stoppingToken);
        var consumeTask = _consumeStep.RunAsync(stoppingToken);

        try
        {
            await produceTask;
        }
        finally
        {
            _queue.Channel.Writer.TryComplete();
        }

        await consumeTask;
    }
}

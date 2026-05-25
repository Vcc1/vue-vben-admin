using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QueryPriceSupRoomBuilder;

public sealed class QueryPriceSupRoomBuildProcessor
{
    readonly IQueryPriceClient _queryPriceClient;
    readonly ISupRoomRedisPublisher _redisPublisher;
    readonly SupRoomCandidateSelector _selector;
    readonly RoomBuildTaskMetrics _metrics;
    readonly IOptionsMonitor<RoomBuildTaskOptions> _optionsMonitor;
    readonly ILogger<QueryPriceSupRoomBuildProcessor> _logger;

    public QueryPriceSupRoomBuildProcessor(
        IQueryPriceClient queryPriceClient,
        ISupRoomRedisPublisher redisPublisher,
        SupRoomCandidateSelector selector,
        RoomBuildTaskMetrics metrics,
        IOptionsMonitor<RoomBuildTaskOptions> optionsMonitor,
        ILogger<QueryPriceSupRoomBuildProcessor> logger)
    {
        _queryPriceClient = queryPriceClient;
        _redisPublisher = redisPublisher;
        _selector = selector;
        _metrics = metrics;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<ProcessTaskResult> ProcessAsync(
        QueryPriceSupRoomBuildTaskModel task,
        CancellationToken cancellationToken)
    {
        var options = _optionsMonitor.CurrentValue;
        var suppliers = NormalizeSuppliers(options.Suppliers);
        var persons = NormalizePersons(options.Persons);
        var windows = RoomBuildTaskParser.ParseDateWindows(options.DateWindows)
            .SelectMany(x => x.Build(DateTime.Now.Date))
            .ToArray();

        foreach (var supId in suppliers)
        {
            foreach (var window in windows)
            {
                foreach (var person in persons)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var response = await _queryPriceClient.QueryPriceAsync(
                        new QueryPriceRequest([task.MTHotelCode], supId.ToString(), person, 0, window.CheckIn, window.CheckOut),
                        cancellationToken);

                    if (response?.ListPrice.Count > 0 != true)
                    {
                        continue;
                    }

                    var rooms = _selector.Select(response.ListPrice, supId, _metrics);
                    if (rooms.Count == 0)
                    {
                        continue;
                    }

                    _metrics.RecordPriceHit(supId, rooms.Count);
                    var pushed = await _redisPublisher.PushAsync(rooms, cancellationToken);
                    _metrics.RecordRedisPush("success", (int)pushed);
                    _logger.LogInformation(
                        "Pushed discovered sup rooms to Redis. MTHotelCode={MTHotelCode}, SupId={SupId}, Count={Count}",
                        task.MTHotelCode,
                        supId,
                        pushed);

                    return ProcessTaskResult.Completed((int)pushed);
                }
            }
        }

        return ProcessTaskResult.NoHit();
    }

    static int[] NormalizeSuppliers(int[] suppliers)
    {
        var normalized = suppliers.Where(x => x > 0).Distinct().ToArray();
        return normalized.Length == 0 ? [51] : normalized;
    }

    static int[] NormalizePersons(int[] persons)
    {
        var normalized = persons.Where(x => x > 0).Distinct().ToArray();
        return normalized.Length == 0 ? [1, 2, 3, 4] : normalized;
    }
}

public sealed record ProcessTaskResult(bool IsCompleted, int QueuedCount)
{
    public static ProcessTaskResult Completed(int queuedCount)
    {
        return new ProcessTaskResult(true, queuedCount);
    }

    public static ProcessTaskResult NoHit()
    {
        return new ProcessTaskResult(false, 0);
    }
}

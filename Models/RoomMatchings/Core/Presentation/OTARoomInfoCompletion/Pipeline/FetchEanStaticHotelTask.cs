using Infrastructure.HTTP.SupStaticData.EAN;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using System.Threading.RateLimiting;

public sealed class FetchEanStaticHotelTask
{
    private const string StageName = "fetch_ean_static_hotels";
    private const string InputQueueName = "resolved_sup_hotels";
    private const string OutputQueueName = "ean_hotels";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FetchEanStaticHotelTask> _logger;
    private readonly OtaRoomInfoCompletionOptions _options;
    private readonly OtaRoomInfoCompletionMetrics _metrics;
    private readonly RateLimiter _rateLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromSeconds(1),
        QueueLimit = int.MaxValue,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true
    });

    public FetchEanStaticHotelTask(IServiceScopeFactory scopeFactory, ILogger<FetchEanStaticHotelTask> logger, OtaRoomInfoCompletionOptions options, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<OtaRoomInfoCompletionWorkItem> hotelReader, ChannelWriter<OtaRoomInfoCompletionWorkItem> eanHotelWriter, CancellationToken cancellationToken)
    {
        var runningTasks = new List<Task>();
        var batch = new List<OtaRoomInfoCompletionWorkItem>(_options.EanBatchSize);

        try
        {
            await foreach (var hotel in hotelReader.ReadAllAsync(cancellationToken))
            {
                _metrics.DecreaseQueueDepth(InputQueueName);
                batch.Add(hotel);
                if (batch.Count < _options.EanBatchSize)
                {
                    continue;
                }
                await StartBatchTaskAsync(batch, runningTasks, eanHotelWriter, cancellationToken);
                batch = new List<OtaRoomInfoCompletionWorkItem>(_options.EanBatchSize);
            }
            if (batch.Count > 0)
            {
                await StartBatchTaskAsync(batch, runningTasks, eanHotelWriter, cancellationToken);
            }
            await WaitAllAsync(runningTasks);
            eanHotelWriter.TryComplete();
        }
        catch (Exception ex)
        {
            eanHotelWriter.TryComplete(ex);
            throw;
        }
    }

    private async Task StartBatchTaskAsync(List<OtaRoomInfoCompletionWorkItem> batch, List<Task> runningTasks, ChannelWriter<OtaRoomInfoCompletionWorkItem> eanHotelWriter, CancellationToken cancellationToken)
    {
        _metrics.IncreaseRunningTasks(StageName);
        runningTasks.Add(ProcessBatchWithMetricsAsync(batch, eanHotelWriter, cancellationToken));
        if (runningTasks.Count < _options.EanFetchConcurrency)
        {
            return;
        }

        var completedTask = await Task.WhenAny(runningTasks);
        //zhe
        runningTasks.Remove(completedTask);
        await completedTask;
    }

    private async Task ProcessBatchWithMetricsAsync(List<OtaRoomInfoCompletionWorkItem> batch, ChannelWriter<OtaRoomInfoCompletionWorkItem> eanHotelWriter, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessBatchAsync(batch, eanHotelWriter, cancellationToken);
            _metrics.RecordCompletedTasks(StageName);
        }
        catch
        {
            _metrics.RecordErrors(StageName);
            throw;
        }
        finally
        {
            _metrics.DecreaseRunningTasks(StageName);
        }
    }

    private async Task ProcessBatchAsync(List<OtaRoomInfoCompletionWorkItem> batch, ChannelWriter<OtaRoomInfoCompletionWorkItem> eanHotelWriter, CancellationToken cancellationToken)
    {
        var validItems = batch
            .Where(x => !string.IsNullOrWhiteSpace(x.SHotelCode))
            .ToList();

        foreach (var item in batch.Except(validItems))
        {
            _logger.LogWarning("SHotelCode 为空，跳过后续流程, MTHotelCode: {MTHotelCode}, SupId: {SupId}", item.MTHotelCode, item.SupId);
        }

        if (validItems.Count == 0)
        {
            return;
        }

        if (_options.SkipEanFetch)
        {
            foreach (var item in validItems)
            {
                await eanHotelWriter.WriteAsync(item, cancellationToken);
                _metrics.IncreaseQueueDepth(OutputQueueName);
            }

            _logger.LogInformation("已跳过 EAN 静态数据请求并直接写入下游队列, BatchSize: {BatchSize}", validItems.Count);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var eanStaticDataContext = scope.ServiceProvider.GetRequiredService<EANStaticDataContext>();

        var sHotelCodes = validItems
            .Select(x => x.SHotelCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();


        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);

        var response = await eanStaticDataContext.GetHotelListAsync(sHotelCodes, cancellationToken: cancellationToken);
        var hotelMap = response.Data
            .Where(x => x.Value != null && (!string.IsNullOrWhiteSpace(x.Key) || !string.IsNullOrWhiteSpace(x.Value.PropertyId)))
            .ToDictionary(
                x => string.IsNullOrWhiteSpace(x.Value.PropertyId) ? x.Key.Trim() : x.Value.PropertyId.Trim(),
                x => x.Value,
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in validItems)
        {
            var sHotelCode = item.SHotelCode.Trim();
            if (!hotelMap.TryGetValue(sHotelCode, out var hotelInfo))
            {
                _logger.LogWarning("未获取到 EAN 静态数据, MTHotelCode: {MTHotelCode}, SHotelCode: {SHotelCode}", item.MTHotelCode, sHotelCode);
            }
            else
            {
                item.HotelInfo = hotelInfo;
            }

            await eanHotelWriter.WriteAsync(item, cancellationToken);
            _metrics.IncreaseQueueDepth(OutputQueueName);
        }
    }

    private static async Task WaitAllAsync(List<Task> runningTasks)
    {
        while (runningTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(runningTasks);
            runningTasks.Remove(completedTask);
            await completedTask;
        }
    }
}

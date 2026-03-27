using Infrastructure.Sql.MengTuYDOTA;
using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public sealed class LoadHotelRoomDataTask
{
    private const string StageName = "load_hotel_room_data";
    private const string InputQueueName = "merged_ean_hotels";
    private const string OutputQueueName = "hotel_room_data";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoadHotelRoomDataTask> _logger;
    private readonly OtaRoomInfoCompletionOptions _options;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public LoadHotelRoomDataTask(IServiceScopeFactory scopeFactory, ILogger<LoadHotelRoomDataTask> logger, OtaRoomInfoCompletionOptions options, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelReader<OtaRoomInfoCompletionWorkItem> eanHotelReader, ChannelWriter<OtaRoomInfoCompletionWorkItem> hotelRoomDataWriter, CancellationToken cancellationToken)
    {
        var runningTasks = new List<Task>();

        try
        {
            await foreach (var item in eanHotelReader.ReadAllAsync(cancellationToken))
            {
                _metrics.DecreaseQueueDepth(InputQueueName);
                _metrics.IncreaseRunningTasks(StageName);
                runningTasks.Add(ProcessItemWithMetricsAsync(item, hotelRoomDataWriter, cancellationToken));
                if (runningTasks.Count < _options.LoadHotelRoomDataConcurrency)
                {
                    continue;
                }

                var completedTask = await Task.WhenAny(runningTasks);
                runningTasks.Remove(completedTask);
                try
                {
                    await completedTask;
                }
                catch (Exception e)
                {
                    var errorCount = _metrics.GetErrorSnapshot().TryGetValue(StageName, out var currentErrorCount)
                        ? currentErrorCount
                        : 0;
                    _logger.LogError(e, "加载酒店房型数据失败, Stage: {StageName}, ErrorCount: {ErrorCount}", StageName, errorCount);
                }

            }

            await WaitAllAsync(runningTasks);
            hotelRoomDataWriter.TryComplete();
        }
        catch (Exception ex)
        {
            hotelRoomDataWriter.TryComplete(ex);
            throw;
        }
    }

    private async Task ProcessItemWithMetricsAsync(OtaRoomInfoCompletionWorkItem item, ChannelWriter<OtaRoomInfoCompletionWorkItem> hotelRoomDataWriter, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessItemAsync(item, hotelRoomDataWriter, cancellationToken);
            _metrics.RecordCompletedTasks(StageName);
        }
        catch (Exception ex)
        {
            _metrics.RecordErrors(StageName);

            _logger.LogError(ex, "加载酒店房型数据异常, Stage: {StageName}, MTHotelCode: {MTHotelCode},", StageName, item.MTHotelCode);
        }
        finally
        {
            _metrics.DecreaseRunningTasks(StageName);
        }
    }

    private async Task ProcessItemAsync(OtaRoomInfoCompletionWorkItem item, ChannelWriter<OtaRoomInfoCompletionWorkItem> hotelRoomDataWriter, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();


        var otaRooms = await dbContext.OTARoomInfos
            .AsNoTracking()
            .Where(x => x.MTHotelCode == item.MTHotelCode)
            .Include(x => x.Extension)
            .ToListAsync(cancellationToken);

        var supRooms = await dbContext.SupRoomInfos
            .AsNoTracking()
            .Where(x => x.MTHotelCode == item.MTHotelCode && x.SupId == 51)

            .ToListAsync(cancellationToken);
        _logger.LogInformation("加载房型数据完成, MTHotelCode: {MTHotelCode}, OtaRooms: {OtaRoomCount}, SupRooms: {SupRoomCount}", item.MTHotelCode, otaRooms.Count, supRooms.Count);
        item.OtaRooms = otaRooms;
        item.SupRooms = supRooms;
        await hotelRoomDataWriter.WriteAsync(item, cancellationToken);
        _metrics.IncreaseQueueDepth(OutputQueueName);
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

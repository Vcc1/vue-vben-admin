using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public sealed class LoadMtHotelCodeQueueTask
{
    private const string StageName = "load_mt_hotel_codes";
    private const string OutputQueueName = "hotel_codes";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoadMtHotelCodeQueueTask> _logger;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public LoadMtHotelCodeQueueTask(IServiceScopeFactory scopeFactory, ILogger<LoadMtHotelCodeQueueTask> logger, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task RunAsync(ChannelWriter<string> hotelCodeWriter, CancellationToken cancellationToken)
    {
        try
        {
            _metrics.IncreaseRunningTasks(StageName);
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
            var hotelCodes = await dbContext.OTARoomInfos
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.MTHotelCode))
                .Select(x => x.MTHotelCode.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Loaded MTHotelCode count for processing: {Count}", hotelCodes.Count);

            foreach (var hotelCode in hotelCodes)
            {
                await hotelCodeWriter.WriteAsync(hotelCode, cancellationToken);
                _metrics.IncreaseQueueDepth(OutputQueueName);
            }

            hotelCodeWriter.TryComplete();
            _metrics.RecordCompletedTasks(StageName);
        }
        catch (Exception ex)
        {
            _metrics.RecordErrors(StageName);
            hotelCodeWriter.TryComplete(ex);
            throw;
        }
        finally
        {
            _metrics.DecreaseRunningTasks(StageName);
        }
    }
}

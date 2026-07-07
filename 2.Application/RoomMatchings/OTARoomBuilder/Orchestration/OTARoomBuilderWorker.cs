using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IOptionsMonitor<OTARoomBuilderOptions> _options;
    readonly OTARoomBuilderMetrics _metrics;
    readonly ILogger<OTARoomBuilderWorker> _logger;
    readonly Channel<OTARoomBuildJob> _jobChannel;
    readonly Channel<HotelBuildQueueItem> _hotelBuildChannel;
    readonly Channel<RoomCreateQueueItem> _roomCreateChannel;

    public OTARoomBuilderWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OTARoomBuilderOptions> options,
        OTARoomBuilderMetrics metrics,
        ILogger<OTARoomBuilderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _metrics = metrics;
        _logger = logger;
        _jobChannel = Channel.CreateUnbounded<OTARoomBuildJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _hotelBuildChannel = Channel.CreateBounded<HotelBuildQueueItem>(CreateBoundedOptions(ClampQueueCapacity(options.CurrentValue.HotelBuildQueueCapacity)));
        _roomCreateChannel = Channel.CreateBounded<RoomCreateQueueItem>(CreateBoundedOptions(ClampQueueCapacity(options.CurrentValue.RoomCreateQueueCapacity)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ThreadPool.SetMinThreads(16, 16);
        _logger.LogInformation("OTARoomBuilder worker started. CpuCount={CpuCount}", Environment.ProcessorCount);

        var fullSyncProducer = RunFullSyncProducerAsync(stoppingToken);
        var incrementalProducer = RunIncrementalProducerAsync(stoppingToken);
        var hotelLoader = RunHotelLoaderAsync(stoppingToken);
        var matcher = RunMatcherAsync(stoppingToken);
        var creator = RunCreatorAsync(stoppingToken);

        await Task.WhenAll(fullSyncProducer, incrementalProducer, hotelLoader, matcher, creator);
    }

    static TimeSpan GetNextDelay(string cronText)
    {
        var cron = CronExpression.Parse(cronText, CronFormat.IncludeSeconds);
        var now = DateTimeOffset.Now;
        var next = cron.GetNextOccurrence(now, TimeZoneInfo.Local);
        return next is null ? TimeSpan.FromMinutes(10) : next.Value - now;
    }

    sealed record OTARoomBuildJob(OTARoomBuildJobType JobType, IReadOnlyList<string> HotelCodes)
    {
        public static OTARoomBuildJob FullSync() => new(OTARoomBuildJobType.FullSync, Array.Empty<string>());

        public static OTARoomBuildJob Incremental(IReadOnlyList<string> hotelCodes) => new(OTARoomBuildJobType.Incremental, hotelCodes);
    }

    enum OTARoomBuildJobType
    {
        FullSync = 1,
        Incremental = 2
    }

    sealed record HotelBuildQueueItem(HotelRoomBuildContext? Hotel, string? OutputMode, TaskCompletionSource? Completion)
    {
        public static HotelBuildQueueItem ForHotel(HotelRoomBuildContext hotel, string outputMode) => new(hotel, outputMode, null);

        public static HotelBuildQueueItem ForCompletion(TaskCompletionSource completion) => new(null, null, completion);
    }

    sealed record RoomCreateQueueItem(List<Infrastructure.Sql.RoomMapping.Entitys.OTARoomInfoPre>? Rooms, string? OutputMode, TaskCompletionSource? Completion)
    {
        public static RoomCreateQueueItem ForHotel(List<Infrastructure.Sql.RoomMapping.Entitys.OTARoomInfoPre> rooms, string outputMode)
            => new(rooms, outputMode, null);

        public static RoomCreateQueueItem ForCompletion(TaskCompletionSource completion)
            => new(null, null, completion);
    }

    static BoundedChannelOptions CreateBoundedOptions(int capacity)
    {
        return new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };
    }

    static int ClampQueueCapacity(int capacity)
    {
        return Math.Clamp(capacity, 1, 100);
    }
}

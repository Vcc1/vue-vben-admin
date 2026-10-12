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
    readonly Channel<StaticDataQueueItem> _staticDataChannel;
    readonly Channel<RoomCreateQueueItem> _roomCreateChannel;
    readonly Channel<AiRoomQueueItem> _aiRoomChannel;
    int _activeStaticDataTasks;

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
        _staticDataChannel = Channel.CreateBounded<StaticDataQueueItem>(CreateBoundedOptions(ClampQueueCapacity(options.CurrentValue.StaticDataQueueCapacity)));
        _roomCreateChannel = Channel.CreateBounded<RoomCreateQueueItem>(CreateBoundedOptions(ClampQueueCapacity(options.CurrentValue.RoomCreateQueueCapacity)));
        _aiRoomChannel = Channel.CreateBounded<AiRoomQueueItem>(CreateBoundedOptions(ClampQueueCapacity(options.CurrentValue.AiRoomQueueCapacity)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ThreadPool.SetMinThreads(16, 16);
        _logger.LogInformation("OTARoomBuilder worker started. CpuCount={CpuCount}", Environment.ProcessorCount);

        var fullSyncProducer = RunFullSyncProducerAsync(stoppingToken);
        var incrementalProducer = RunIncrementalProducerAsync(stoppingToken);
        var hotelLoader = RunHotelLoaderAsync(stoppingToken);
        var matcher = RunMatcherAsync(stoppingToken);
        var staticDataLoader = RunStaticDataAsync(stoppingToken);
        var creator = RunCreatorAsync(stoppingToken);
        var aiRoomBuilder = RunAiRoomAsync(stoppingToken);

        await Task.WhenAll(fullSyncProducer, incrementalProducer, hotelLoader, matcher, staticDataLoader, creator, aiRoomBuilder);
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

    sealed record StaticDataQueueItem(HotelRoomBuildContext? Hotel, HotelRoomMatchResult? MatchResult, string? OutputMode, TaskCompletionSource? Completion)
    {
        public static StaticDataQueueItem ForHotel(HotelRoomBuildContext hotel, HotelRoomMatchResult matchResult, string outputMode)
            => new(hotel, matchResult, outputMode, null);

        public static StaticDataQueueItem ForCompletion(TaskCompletionSource completion)
            => new(null, null, null, completion);
    }

    sealed record RoomCreateQueueItem(HotelRoomBuildContext? Hotel, List<Infrastructure.Sql.RoomMapping.Entitys.OTARoomInfoPre>? Rooms, string? OutputMode, TaskCompletionSource? Completion)
    {
        public static RoomCreateQueueItem ForHotel(HotelRoomBuildContext hotel, List<Infrastructure.Sql.RoomMapping.Entitys.OTARoomInfoPre> rooms, string outputMode)
            => new(hotel, rooms, outputMode, null);

        public static RoomCreateQueueItem ForCompletion(TaskCompletionSource completion)
            => new(null, null, null, completion);
    }

    sealed record AiRoomQueueItem(HotelRoomBuildContext? Hotel, IReadOnlySet<int>? SuccessfullyCreatedSupplierRoomIds, string? OutputMode, TaskCompletionSource? Completion)
    {
        public static AiRoomQueueItem ForHotel(HotelRoomBuildContext hotel, IReadOnlySet<int> successfullyCreatedSupplierRoomIds, string outputMode)
            => new(hotel, successfullyCreatedSupplierRoomIds, outputMode, null);

        public static AiRoomQueueItem ForCompletion(TaskCompletionSource completion)
            => new(null, null, null, completion);
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

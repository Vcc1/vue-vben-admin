using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

public sealed class OtaRoomInfoCompletionPipeline
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OtaRoomInfoCompletionOptions _options;
    private readonly OtaRoomInfoCompletionMetrics _metrics;

    public OtaRoomInfoCompletionPipeline(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory, OtaRoomInfoCompletionOptions options, OtaRoomInfoCompletionMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _options = options;
        _metrics = metrics;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var channels = new OtaRoomInfoCompletionChannels(_options);
        RegisterQueueDepthProviders(channels);

        var loadMtHotelCodeQueueTask = new LoadMtHotelCodeQueueTask(_scopeFactory, _loggerFactory.CreateLogger<LoadMtHotelCodeQueueTask>(), _metrics);
        var resolveSupHotelMappingTask = new ResolveSupHotelMappingTask(_scopeFactory, _loggerFactory.CreateLogger<ResolveSupHotelMappingTask>(), _options, _metrics);
        var fetchEanStaticHotelTask = new FetchEanStaticHotelTask(_scopeFactory, _loggerFactory.CreateLogger<FetchEanStaticHotelTask>(), _options, _metrics);
        var mergeSupStaticRoomInfoTask = new MergeSupStaticRoomInfoTask(_scopeFactory, _loggerFactory.CreateLogger<MergeSupStaticRoomInfoTask>(), _metrics);
        //var discardMergedEanHotelsTask = new DiscardMergedEanHotelsTask(_loggerFactory.CreateLogger<DiscardMergedEanHotelsTask>(), _metrics);
        var loadHotelRoomDataTask = new LoadHotelRoomDataTask(_scopeFactory, _loggerFactory.CreateLogger<LoadHotelRoomDataTask>(), _options, _metrics);
        var parseHotelRoomTask = new ParseHotelRoomTask(_scopeFactory, _loggerFactory.CreateLogger<ParseHotelRoomTask>(), _options, _metrics);
        var saveParsedHotelBatchTask = new SaveParsedHotelBatchTask(_scopeFactory, _loggerFactory.CreateLogger<SaveParsedHotelBatchTask>(), _options, _metrics);

        var tasks = new Task[]
        {
            loadMtHotelCodeQueueTask.RunAsync(channels.HotelCodes.Writer, cancellationToken),
            resolveSupHotelMappingTask.RunAsync(channels.HotelCodes.Reader, channels.ResolvedSupHotels.Writer, cancellationToken),
            fetchEanStaticHotelTask.RunAsync(channels.ResolvedSupHotels.Reader, channels.EanHotels.Writer, cancellationToken),
            mergeSupStaticRoomInfoTask.RunAsync(channels.EanHotels.Reader, channels.MergedEanHotels.Writer, cancellationToken),
            //discardMergedEanHotelsTask.RunAsync(channels.MergedEanHotels.Reader, cancellationToken),
            loadHotelRoomDataTask.RunAsync(channels.MergedEanHotels.Reader, channels.HotelRoomData.Writer, cancellationToken),
            parseHotelRoomTask.RunAsync(channels.HotelRoomData.Reader, channels.ParsedHotels.Writer, cancellationToken),
            saveParsedHotelBatchTask.RunAsync(channels.ParsedHotels.Reader, cancellationToken)
        };
        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            _metrics.ClearQueueDepthProviders();
        }
    }

    private void RegisterQueueDepthProviders(OtaRoomInfoCompletionChannels channels)
    {
        _metrics.RegisterQueueDepthProvider("hotel_codes", () => GetQueueDepth(channels.HotelCodes.Reader));
        _metrics.RegisterQueueDepthProvider("resolved_sup_hotels", () => GetQueueDepth(channels.ResolvedSupHotels.Reader));
        _metrics.RegisterQueueDepthProvider("ean_hotels", () => GetQueueDepth(channels.EanHotels.Reader));
        _metrics.RegisterQueueDepthProvider("merged_ean_hotels", () => GetQueueDepth(channels.MergedEanHotels.Reader));
        _metrics.RegisterQueueDepthProvider("hotel_room_data", () => GetQueueDepth(channels.HotelRoomData.Reader));
        _metrics.RegisterQueueDepthProvider("parsed_hotels", () => GetQueueDepth(channels.ParsedHotels.Reader));
    }

    private static long GetQueueDepth<T>(ChannelReader<T> reader)
    {
        return reader.CanCount ? reader.Count : 0;
    }
}

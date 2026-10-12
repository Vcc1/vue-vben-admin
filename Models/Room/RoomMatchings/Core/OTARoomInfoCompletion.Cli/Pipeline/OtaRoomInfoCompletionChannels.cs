using System.Threading.Channels;

public sealed class OtaRoomInfoCompletionChannels
{
    private const int MaxPipelineQueueCapacity = 50;

    public OtaRoomInfoCompletionChannels(OtaRoomInfoCompletionOptions options)
    {
        HotelCodes = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
        ResolvedSupHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(ClampPipelineQueueCapacity(options.EanHotelQueueCapacity)));
        EanHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(ClampPipelineQueueCapacity(options.EanHotelQueueCapacity)));
        MergedEanHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(ClampPipelineQueueCapacity(options.EanHotelQueueCapacity)));
        HotelRoomData = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(ClampPipelineQueueCapacity(options.HotelRoomDataQueueCapacity)));
        ParsedHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(ClampPipelineQueueCapacity(options.ParsedHotelQueueCapacity)));
    }

    public Channel<string> HotelCodes { get; }

    public Channel<OtaRoomInfoCompletionWorkItem> ResolvedSupHotels { get; }

    public Channel<OtaRoomInfoCompletionWorkItem> EanHotels { get; }

    public Channel<OtaRoomInfoCompletionWorkItem> MergedEanHotels { get; }

    public Channel<OtaRoomInfoCompletionWorkItem> HotelRoomData { get; }

    public Channel<OtaRoomInfoCompletionWorkItem> ParsedHotels { get; }

    private static BoundedChannelOptions CreateOptions(int capacity)
    {
        return new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };
    }

    private static int ClampPipelineQueueCapacity(int capacity)
    {
        return Math.Min(Math.Max(1, capacity), MaxPipelineQueueCapacity);
    }
}

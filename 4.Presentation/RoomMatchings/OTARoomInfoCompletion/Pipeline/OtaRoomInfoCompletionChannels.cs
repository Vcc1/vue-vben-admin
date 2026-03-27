using System.Threading.Channels;

public sealed class OtaRoomInfoCompletionChannels
{
    public OtaRoomInfoCompletionChannels(OtaRoomInfoCompletionOptions options)
    {
        HotelCodes = Channel.CreateBounded<string>(CreateOptions(options.HotelCodeQueueCapacity));
        ResolvedSupHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(options.EanHotelQueueCapacity));
        EanHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(options.EanHotelQueueCapacity));
        MergedEanHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(options.EanHotelQueueCapacity));
        HotelRoomData = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(options.HotelRoomDataQueueCapacity));
        ParsedHotels = Channel.CreateBounded<OtaRoomInfoCompletionWorkItem>(CreateOptions(options.ParsedHotelQueueCapacity));
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
}

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoIngestionQueue
{
    public SupRoomInfoIngestionQueue(IOptions<SupRoomInfoIngestionOptions> options)
    {
        var current = options.Value;
        Channel = System.Threading.Channels.Channel.CreateBounded<TempSupRoomInfo2>(new BoundedChannelOptions(current.WriteBatchSize * 3)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
    }

    public Channel<TempSupRoomInfo2> Channel { get; }
}

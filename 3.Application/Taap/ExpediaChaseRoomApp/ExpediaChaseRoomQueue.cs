using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace ExpediaChaseRoomApp;

public sealed class ExpediaChaseRoomQueue
{
    public ExpediaChaseRoomQueue(IOptions<ExpediaChaseRoomAppOptions> options)
    {
        Channel = System.Threading.Channels.Channel.CreateBounded<ExpediaChaseRoomTask>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });
    }

    public Channel<ExpediaChaseRoomTask> Channel { get; }
}

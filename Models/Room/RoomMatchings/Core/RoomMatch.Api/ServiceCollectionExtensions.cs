using Microsoft.Extensions.DependencyInjection;

namespace RoomMatch.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddRoomMatchApi(this IMvcBuilder builder)
        => builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
}

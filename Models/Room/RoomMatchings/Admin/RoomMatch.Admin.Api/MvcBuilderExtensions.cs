using Microsoft.Extensions.DependencyInjection;

namespace RoomMatch.Admin.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddRoomMatchAdminApi(this IMvcBuilder builder)
        => builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
}

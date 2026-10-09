using Microsoft.Extensions.DependencyInjection;
using RoomMatch.Admin.Contracts;

namespace RoomMatch.Admin.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomMatchAdminApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<WordApp>();

        return services;
    }
}

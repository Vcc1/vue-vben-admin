using Microsoft.Extensions.DependencyInjection;

namespace RoomMatch.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomMatchApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<OTAHotelRoomApp>();

        return services;
    }
}

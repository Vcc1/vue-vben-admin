using Microsoft.Extensions.DependencyInjection;
using RoomMatch.Admin.Contracts;

namespace RoomMatch.Admin.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoomMatchAdminInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IWordRepository, WordRepository>();

        return services;
    }
}

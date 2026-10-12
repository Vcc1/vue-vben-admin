using Microsoft.Extensions.DependencyInjection;
using OTARoomBuilder.Contracts;

namespace Infrastructure.HTTP.OTARoomCreate;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOtaRoomCreateHttp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IOtaRoomCreateClient, OtaRoomCreateClient>();
        return services;
    }
}

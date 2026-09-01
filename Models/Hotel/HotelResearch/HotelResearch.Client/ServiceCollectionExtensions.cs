using HotelResearch.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HotelResearch.Client;

public static class ServiceCollectionExtensions
{
    public static IHttpClientBuilder AddHotelFactsClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureClient);
        return services.AddHttpClient<IHotelFactsClient, HttpHotelFactsClient>(configureClient);
    }
}

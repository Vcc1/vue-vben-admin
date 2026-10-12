using HotelResearch.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HotelResearch.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelResearchApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IHotelResearchService, HotelResearchService>();
        return services;
    }
}

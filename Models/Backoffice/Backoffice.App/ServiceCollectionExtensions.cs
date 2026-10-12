using Backoffice.App.HotelPriceComparison;
using Microsoft.Extensions.DependencyInjection;

namespace Backoffice.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackofficeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<HotelPriceComparisonRunHistoryService>();
        return services;
    }
}

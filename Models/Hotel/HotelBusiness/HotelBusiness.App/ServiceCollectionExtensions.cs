using Microsoft.Extensions.DependencyInjection;

namespace HotelBusiness.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelBusinessApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IHotelBusinessService, HotelBusinessService>();
        services.AddSingleton<IHotelOperationConcurrencyGuard, HotelOperationConcurrencyGuard>();
        return services;
    }
}

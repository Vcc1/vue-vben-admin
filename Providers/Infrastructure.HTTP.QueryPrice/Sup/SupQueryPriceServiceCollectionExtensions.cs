using Microsoft.Extensions.DependencyInjection;
using QueryPriceSupRoomBuilder;

namespace Infrastructure.HTTP.QueryPrice.Sup;

public static class SupQueryPriceServiceCollectionExtensions
{
    public static IServiceCollection AddSupQueryPrice(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(SupQueryPriceApi));
        services.AddSingleton<SupQueryPriceMetrics>();
        services.AddSingleton<SupQueryPriceApi>();
        services.AddSingleton<IQueryPriceClient>(sp => sp.GetRequiredService<SupQueryPriceApi>());
        return services;
    }
}

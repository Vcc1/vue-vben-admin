using Infrastructure.SupplierStaticData.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupplierStaticDataApp;
using SupplierStaticDataApp.Contracts;

namespace Infrastructure.SupplierStaticData;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSupplierStaticDataInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SupHotelStaticDataOptions>()
            .Bind(configuration.GetSection("SupHotelStaticData"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<EanHotelStaticDataClient>()
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli
            });
        services
            .AddHttpClient<AgodaHotelStaticDataClient>()
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli
            });
        services
            .AddHttpClient<AgodaRoomStaticDataClient>()
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli
            });
        services
            .AddHttpClient<HbStaticDataClient>()
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli
            });
        services.AddScoped<ISupplierStaticDataService, SupplierStaticDataService>();
        services.AddSingleton<SupplierStaticDataMetrics>();
        services.AddScoped<ISupplierStaticDataCacheRepository, SupplierStaticDataCacheRepository>();
        services.AddScoped<ISupplierStaticDataProvider, EanSupplierStaticDataProvider>();
        services.AddScoped<ISupplierStaticDataProvider, AgodaSupplierStaticDataProvider>();
        services.AddScoped<ISupplierStaticDataProvider, HbSupplierStaticDataProvider>();
        return services;
    }
}

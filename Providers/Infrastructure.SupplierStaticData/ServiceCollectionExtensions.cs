using Infrastructure.SupplierStaticData.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
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

        ConfigureStaticDataHttpClient(services.AddHttpClient<EanHotelStaticDataClient>());
        ConfigureStaticDataHttpClient(services.AddHttpClient<AgodaHotelStaticDataClient>());
        ConfigureStaticDataHttpClient(services.AddHttpClient<AgodaRoomStaticDataClient>());
        ConfigureStaticDataHttpClient(services.AddHttpClient<HbStaticDataClient>());
        services.AddScoped<ISupplierStaticDataService, SupplierStaticDataService>();
        services.AddSingleton<SupplierStaticDataMetrics>();
        services.AddScoped<ISupplierStaticDataCacheRepository, SupplierStaticDataCacheRepository>();
        services.AddScoped<ISupplierStaticDataProvider, EanSupplierStaticDataProvider>();
        services.AddScoped<ISupplierStaticDataProvider, AgodaSupplierStaticDataProvider>();
        services.AddScoped<ISupplierStaticDataProvider, HbSupplierStaticDataProvider>();
        return services;
    }

    static void ConfigureStaticDataHttpClient(IHttpClientBuilder builder)
    {
        var timeout = TimeSpan.FromMinutes(5);

        builder.ConfigureHttpClient(client => client.Timeout = timeout);

#pragma warning disable EXTEXP0001
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = timeout;
            options.AttemptTimeout.Timeout = timeout;
            options.CircuitBreaker.SamplingDuration = timeout * 2;
        });

        builder.ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                | System.Net.DecompressionMethods.Deflate
                | System.Net.DecompressionMethods.Brotli
        });
    }
}

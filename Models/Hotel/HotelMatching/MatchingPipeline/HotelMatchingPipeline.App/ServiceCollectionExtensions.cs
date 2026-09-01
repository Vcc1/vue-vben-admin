using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Domain.HotelNameMatching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelMatchingPipeline.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelMatchingApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddSharedHotelServices(services, configuration);
        AddHotelCleaningServicesCore(services, configuration);
        AddHotelMatchingServicesCore(services, configuration);
        return services;
    }

    public static IServiceCollection AddHotelCleaningServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddSharedHotelServices(services, configuration);
        AddHotelCleaningServicesCore(services, configuration);
        return services;
    }

    public static IServiceCollection AddHotelMatchingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddSharedHotelServices(services, configuration);
        AddHotelMatchingServicesCore(services, configuration);
        return services;
    }

    static void AddSharedHotelServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HotelStarLevelOptions>()
            .Bind(configuration.GetSection(HotelStarLevelOptions.SectionName))
            .ValidateDataAnnotations();
        services.TryAddSingleton<HotelNameMatcher>();
        services.TryAddSingleton<IHotelPhoneNormalizer, LibPhoneNumberNormalizer>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHotelStarLevelConverter, ConfiguredSupplierStarLevelConverter>());
        services.TryAddSingleton<IHotelStarLevelNormalizer, HotelStarLevelNormalizer>();
    }

    static void AddHotelCleaningServicesCore(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HotelCleaningOptions>()
            .Bind(configuration.GetSection(HotelCleaningOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddScoped<HotelCleaner>();
        services.AddScoped<HotelCleaningService>();
    }

    static void AddHotelMatchingServicesCore(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HotelMatchingOptions>()
            .Bind(configuration.GetSection(HotelMatchingOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddSingleton<HotelMatchEvaluator>();
        services.AddSingleton<HotelFactMatchEvaluator>();
        services.AddScoped<HotelMatchingService>();
        services.AddScoped<HotelDebugService>();
    }
}

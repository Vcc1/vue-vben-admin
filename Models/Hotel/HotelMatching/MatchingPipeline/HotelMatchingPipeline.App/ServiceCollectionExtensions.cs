using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Domain.HotelNameMatching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelMatchingPipeline.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHotelMatchingApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HotelCleaningOptions>()
            .Bind(configuration.GetSection(HotelCleaningOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddOptions<HotelMatchingOptions>()
            .Bind(configuration.GetSection(HotelMatchingOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddOptions<HotelStarLevelOptions>()
            .Bind(configuration.GetSection(HotelStarLevelOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddSingleton<HotelNameMatcher>();
        services.AddSingleton<IHotelPhoneNormalizer, LibPhoneNumberNormalizer>();
        services.AddSingleton<IHotelStarLevelConverter, ConfiguredSupplierStarLevelConverter>();
        services.AddSingleton<IHotelStarLevelNormalizer, HotelStarLevelNormalizer>();
        services.AddSingleton<HotelMatchEvaluator>();
        services.AddScoped<HotelCleaner>();
        services.AddScoped<HotelCleaningService>();
        services.AddScoped<HotelMatchingService>();
        services.AddScoped<HotelDebugService>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace HotelResearch.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddHotelResearchApi(this IMvcBuilder builder)
        => builder.AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
}

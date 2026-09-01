using Microsoft.Extensions.DependencyInjection;

namespace HotelBusiness.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddHotelBusinessApi(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddApplicationPart(typeof(HotelBusinessController).Assembly);
        return builder;
    }
}

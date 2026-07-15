using Framework.Core.IOC;
using Infrastructure.HTTP.OTAHotelInfo;
using Infrastructure.Sql.RoomMapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OTAHotelInfoSync.Contracts;

namespace Infrastructure.OTAHotelInfoSync;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOTAHotelInfoSyncInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        ServiceCollectionExpand.AddBusiness(
            services,
            [typeof(RoomMappingDbContext).Assembly]);

        services.AddOtaHotelInfoHttp(options =>
            configuration.GetSection("OTAHotelInfo").Bind(options));
        services.AddScoped<IOTAHotelInfoSource, OtaHotelInfoSource>();
        services.AddScoped<IOTAHotelInfoRepository, RoomMappingOTAHotelInfoRepository>();
        return services;
    }
}

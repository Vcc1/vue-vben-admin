using Framework.Core.IOC;
using Infrastructure.HTTP.OTAHotelInfo;
using Infrastructure.HTTP.WorkWechatAlert;
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
        services.AddWorkWechatAlertHttpProvider();
        services.AddScoped<IOTAHotelInfoSource, OtaHotelInfoSource>();
        services.AddScoped<IOTAHotelInfoRepository, RoomMappingOTAHotelInfoRepository>();
        services.AddSingleton<IOTAHotelInfoSyncNotifier, WorkWechatOTAHotelInfoSyncNotifier>();
        return services;
    }
}

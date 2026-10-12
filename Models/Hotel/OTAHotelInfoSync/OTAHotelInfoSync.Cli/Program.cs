using Infrastructure.OTAHotelInfoSync;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.Hosting;
using OTAHotelInfoSync;
using VBaseFramework.ServiceDefaults;

Environment.SetEnvironmentVariable("TZ", "Asia/Shanghai");
TimeZoneInfo.ClearCachedData();

var host = Serve.Run(new RunOptions
{
    Args = args,
    ServiceName = "MT-OTAHotelInfoSync",
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddOTAHotelInfoSyncInfrastructure(configuration);
    services.AddOTAHotelInfoSync(configuration);
});

await host.RunAsync();

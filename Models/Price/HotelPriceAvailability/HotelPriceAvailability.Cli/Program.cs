using Framework.Core.IOC;
using HotelPriceAvailability.App;
using HotelPriceAvailability.Cli;
using HotelPriceAvailability.Infra;
using Infrastructure.Sql.RoomMapping;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using VBase.AppMetrics;
using VBaseFramework.ServiceDefaults;

Environment.SetEnvironmentVariable("TZ", "Asia/Shanghai");
TimeZoneInfo.ClearCachedData();

var processModule = Process.GetCurrentProcess().MainModule;
if (processModule is not null)
{
    Directory.SetCurrentDirectory(Path.GetDirectoryName(processModule.FileName)!);
}

var exitState = new JobExitState();
var host = Serve.Run(
    new RunOptions { Args = args, ServiceName = "MT-HotelPriceAvailability" },
    (services, configuration) =>
    {
        services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
        services.AddConnectmateRemoteConfiguration(configuration);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAppMetrics(configuration);
        services.AddHotelPriceAvailabilityApplication(configuration)
            .AddHotelPriceAvailabilityInfrastructure(configuration);
        services.AddBusiness([typeof(RoomMappingDbContext).Assembly]);
        services.AddSingleton(exitState);
        services.AddHostedService<HotelPriceAvailabilityWorker>();
    });

await host.RunAsync();
Environment.ExitCode = exitState.ExitCode;

using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomMatch.Core;
using System.Diagnostics;
using VBase.AppMetrics;
using VBaseFramework.ServiceDefaults;

var processModule = Process.GetCurrentProcess().MainModule;
if (processModule is not null)
{
    var pathToContentRoot = Path.GetDirectoryName(processModule.FileName)!;
    Directory.SetCurrentDirectory(pathToContentRoot);
}

var host = Serve.Run(new RunOptions
{
    Args = args,
    ServiceName = "MT-RoomMatchingStatistics",
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddAppMetrics(configuration, options =>
    {
        options.ServiceName = "RoomMatchingStatistics";
        options.MeterName = "AppMetrics";
        options.AdditionalMeterNames = options.AdditionalMeterNames
            .Concat(new[] { RoomMatchingStatisticsMetrics.ScopeName })
            .Distinct()
            .ToArray();
    });
    services.Configure<RoomMatchingStatisticsOptions>(configuration.GetSection(RoomMatchingStatisticsOptions.SectionName));
    services.AddRoomMatchInfrastructure(configuration);
    services.AddSingleton<RoomMatchingStatisticsMetrics>();
    services.AddHostedService<RoomMatchingStatisticsWorker>();
}, (WebApplication app) =>
{
});

await host.RunAsync();

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
    ServiceName = "MT-RoomMatching",
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddAppMetrics(configuration, options =>
    {
        options.ServiceName = "RoomMatching";
        options.MeterName = "AppMetrics";
        options.AdditionalMeterNames = options.AdditionalMeterNames
            .Concat(new[] { "RoomMatching" })
            .Distinct()
            .ToArray();
    });
    services.AddRoomMatchInfrastructure(configuration);
    services.AddSingleton<RoomMatchingMetrics>();
    services.AddHostedService<Worker>();
}, (WebApplication app) =>
{
});

await host.RunAsync();

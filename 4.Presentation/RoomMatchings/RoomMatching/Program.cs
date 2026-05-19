using MengTu.Connectmate.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomMatch.Core;
using System.Diagnostics;
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
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddRoomMatchInfrastructure(configuration);
    services.AddHostedService<Worker>();
}, (WebApplication app) =>
{
});

await host.RunAsync();

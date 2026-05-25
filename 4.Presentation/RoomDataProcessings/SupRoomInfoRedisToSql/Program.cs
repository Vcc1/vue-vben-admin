using Framework.Core.IOC;
using Infrastructure.SupRoomInfoIngestion;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomMatch.Core;
using SupRoomInfoIngestion;
using System.Diagnostics;
using System.Reflection;
using VBase.AppMetrics;
using VBaseFramework.Core.IOC;
using VBaseFramework.Core.Redis;
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
    ServiceName = "MT-SupRoomInfoRedisToSql",
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services
        .AddAppMetrics(configuration)
        .AddRoomMatchInfrastructure(configuration)
        .AddSupRoomInfoIngestion(configuration)
        .AddSupRoomInfoIngestionInfrastructure()
        .AddOptions(configuration, new Assembly[]
        {
            typeof(SupRoomInfoIngestionOptions).Assembly,
        })
        .AddBusiness(Array.Empty<Assembly>());
});

await host.RunAsync();

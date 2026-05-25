using Framework.Core.IOC;
using Infrastructure.HTTP.QueryPrice.Sup;
using Infrastructure.QueryPriceSupRoomBuilder;
using Infrastructure.Redis.TriggerQueue;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QueryPriceSupRoomBuilder;
using RoomMatch.Core;
using System.Diagnostics;
using System.Reflection;
using VBase.AppMetrics;
using VBaseFramework.Core.IOC;
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
    ServiceName = "MT-QueryPriceSupRoomBuilderToRedis",
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services
        .AddAppMetrics(configuration)
        .AddRoomMatchInfrastructure(configuration)
        .AddQueryPriceSupRoomBuilder(configuration)
        .AddQueryPriceSupRoomBuilderInfrastructure()
        .AddSupQueryPrice()
        .AddTriggerQueueRedis()
        .AddOptions(configuration, new Assembly[]
        {
            typeof(RoomBuildTaskOptions).Assembly,
        })
        .AddBusiness(Array.Empty<Assembly>());
});

await host.RunAsync();

using Framework.Core.IOC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomMatch.Core;
using RoomMatch.Core.RoomDictionary;
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
    ServiceName = "MT-OTARoomInfoCompletion",
}, (services, configuration) =>
{
    DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "MengTuAPI");
    DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "RoomMapping");
    services
        .AddAppMetrics(configuration)
        .AddRoomMatchInfrastructure(configuration)
        .AddOptions(configuration, new Assembly[]
        {
            typeof(Worker).Assembly,
            typeof(RoomWord).Assembly,
            typeof(RoomDictionarySingleton).Assembly,
        })
        .AddBusiness(new Assembly[]
        {
            typeof(RoomWord).Assembly,
            typeof(RoomDictionarySingleton).Assembly,
        });

    services.AddSingleton<OtaRoomInfoCompletionMetrics>();
    services.AddHostedService<Worker>();
});

await host.RunAsync();

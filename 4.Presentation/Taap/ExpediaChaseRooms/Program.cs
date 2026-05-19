using System.Diagnostics;
using ExpediaChaseRoomApp;
using VBase.AppMetrics;
using VBaseFramework.ServiceDefaults;

var processModule = Process.GetCurrentProcess().MainModule;
if (processModule is not null)
{
    var pathToContentRoot = Path.GetDirectoryName(processModule.FileName)!;
    Directory.SetCurrentDirectory(pathToContentRoot);
}

var host = Serve.Run(
    new RunOptions { Args = args, ServiceName = "MT-ExpediaChaseRooms" },
    (services, configuration) =>
    {
        DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "SupCacheDB");
        services
            .AddAppMetrics(configuration)
            .AddExpediaChaseRoomApp(configuration)
            .AddExpediaChaseRoomInfrastructure(configuration);
    });

await host.RunAsync();

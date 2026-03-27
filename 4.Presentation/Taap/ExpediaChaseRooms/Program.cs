using System.Diagnostics;
using ExpediaChaseRoomApp;
using Infrastructure.Sql.SupCacheDB;
using System.Reflection;
using VBaseFramework.ServiceDefaults;
using Framework.Core.IOC;
using VBase.AppMetrics;



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
            ServiceCollectionExpand.AddBusiness(services, [typeof(SupCacheDbContext).Assembly]);
            services
                .AddAppMetrics(configuration)
                .AddExpediaChaseRoomApp(configuration);
        });

await host.RunAsync();

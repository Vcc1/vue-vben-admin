using Framework.Core.IOC;
using Infrastructure.Redis.SupRoomInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Reflection;
using VBaseFramework.ServiceDefaults;
using VBaseFramework.Core.IOC;
using VBaseFramework.Core.Redis;
using VBase.AppMetrics;


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
    DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "RoomMapping");
    services
        .AddAppMetrics(configuration)
        .AddOptions(configuration, new Assembly[]
        {
            typeof(Worker).Assembly,
        })
        .AddBusiness(new Assembly[]
        {
            typeof(SupRoomInfoDBContext).Assembly
        });

    services.AddSingleton<SupRoomInfoRedisToSqlMetrics>();
    services.AddHostedService<Worker>();
});

await host.RunAsync();

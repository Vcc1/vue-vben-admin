using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using TestMetrics;
using TestMetrics.TestMetrics;
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
    ServiceName = "MT-SupRoomInfoRedisToSql",
}, (services, configuration) =>
{
    services.AddSingleton<GoodsMetrics>();
    services
        .AddAppMetrics(configuration);
    services.AddHostedService<GoodsMetricsTestWorker>();
});

await host.RunAsync();
using Infrastructure.OTAHotelInfoSync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OTAHotelInfoSync;
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
    ServiceName = "MT-OTAHotelInfoSync",
}, (services, configuration) =>
{
    services.AddOTAHotelInfoSyncInfrastructure(configuration);
    services.AddOTAHotelInfoSync(configuration);
});

await host.RunAsync();

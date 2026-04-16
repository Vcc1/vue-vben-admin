
using Infrastructure.Sql.MengTuAPI;
using Infrastructure.Sql.RoomMapping;
using MengTu.Connectmate.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomMatch.Core.RoomAnalysis;
using System.Diagnostics;
using System.Reflection;
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

    Framework.Core.IOC.ServiceCollectionExpand.AddBusiness(services, new Assembly[] {
         typeof(RoomMappingDbContext).Assembly,
         typeof(MengTuAPIDbContext).Assembly,
         typeof(RoomAnalyzer).Assembly,
         typeof(RoomMatch.Core.RoomDictionary.RoomDictionarySingleton).Assembly
    });

    services.AddHostedService<Worker>();
}, (WebApplication app) =>
{
});

await host.RunAsync();

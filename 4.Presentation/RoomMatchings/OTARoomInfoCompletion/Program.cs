using Framework.Core.IOC;
using Infrastructure.HTTP.SupStaticData.EAN;
using Infrastructure.Sql.MengTuAPI;
using Infrastructure.Sql.RoomMapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        .AddOptions(configuration, new Assembly[]
        {
            typeof(Worker).Assembly,
            typeof(RoomMatch.Core.RoomDictionary.RoomWord).Assembly,
            typeof(RoomDictionarySingleton).Assembly,

        })
        .AddBusiness(new Assembly[]
        {
            typeof(MengTuAPIDbContext).Assembly,
            typeof(RoomMappingDbContext).Assembly,
            typeof(EANStaticDataContext).Assembly,
            typeof(RoomMatch.Core.RoomDictionary.RoomWord).Assembly,
            typeof(RoomDictionarySingleton).Assembly,
        });

    services.AddSingleton<OtaRoomInfoCompletionMetrics>();
    services.AddHostedService<Worker>();
});

await host.RunAsync();

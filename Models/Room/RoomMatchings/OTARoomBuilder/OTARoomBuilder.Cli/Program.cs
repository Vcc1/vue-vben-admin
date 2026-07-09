using Framework.Core.IOC;
using Infrastructure.HTTP.OTARoomCreate;
using Infrastructure.OTARoomBuilder;
using Infrastructure.SupplierStaticData;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.Hosting;
using OTARoomBuilder;
using RoomMatch.Core;
using RoomMatch.Core.RoomDictionary;
using SupplierStaticDataApp;
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
    ServiceName = "MT-OTARoomBuilder",
}, (services, configuration) =>
{
    services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddAppMetrics(configuration, options =>
    {
        options.ServiceName = "OTARoomBuilder";
        options.MeterName = "AppMetrics";
        options.AdditionalMeterNames = options.AdditionalMeterNames
            .Concat([OTARoomBuilderMetrics.ScopeName, SupplierStaticDataMetrics.ScopeName])
            .Distinct()
            .ToArray();
    });

    services
        .AddRoomMatchInfrastructure(configuration)
        .AddSupplierStaticDataInfrastructure(configuration)
        .AddOtaRoomBuilderInfrastructure()
        .AddOtaRoomCreateHttp()
        .AddOtaRoomBuilder(configuration)
        .AddOptions(configuration, [
            typeof(RoomWord).Assembly,
            typeof(RoomDictionarySingleton).Assembly,
            typeof(OTARoomBuilderWorker).Assembly
        ])
        .AddBusiness([
            typeof(RoomWord).Assembly,
            typeof(RoomDictionarySingleton).Assembly
        ]);
});
await host.RunAsync();

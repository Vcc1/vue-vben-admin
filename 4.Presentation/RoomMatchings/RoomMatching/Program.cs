
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Infrastructure.Sql.MengTuYDOTA;
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
    ServiceName = "MT-RoomTypeMatchingTest",
}, (services, configuration) =>
{
    #region 业务SQL注入
    //DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "MT_YD_OTA");
    //DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "MengTuSup");
    //DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "MengTuAPI");
    DbTool.MengTuDBConfigTool.InitDBConfig(configuration, "RoomMapping");
    #endregion


    Framework.Core.IOC.ServiceCollectionExpand.AddBusiness(services, new Assembly[] {
         //typeof(VBaseContext).Assembly,
         typeof(RoomMappingDbContext).Assembly,
         typeof(RoomAnalyzer).Assembly,
         typeof(RoomMatch.Core.RoomDictionary.RoomDictionarySingleton).Assembly
    });

    services.AddHostedService<Worker>();
}, (WebApplication app) =>
{
});

await host.RunAsync();

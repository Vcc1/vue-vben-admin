using DbTool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoomMatch.Core;
using System.Diagnostics;

var processModule = Process.GetCurrentProcess().MainModule;
if (processModule is not null)
{
    var pathToContentRoot = Path.GetDirectoryName(processModule.FileName)!;
    Directory.SetCurrentDirectory(pathToContentRoot);
}

var builder = Host.CreateApplicationBuilder(args);
MengTuDBConfigTool.InitDBConfig(builder.Configuration, "RoomMapping");

builder.Services.AddRoomMatchInfrastructure(builder.Configuration);
builder.Services.AddHostedService<RoomMappingSupStaticRoomInfoStatisticsWorker>();

await builder.Build().RunAsync();

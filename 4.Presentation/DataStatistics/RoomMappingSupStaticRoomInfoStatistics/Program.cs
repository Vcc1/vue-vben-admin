using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
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
builder.Services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
builder.Services.AddConnectmateRemoteConfiguration(builder.Configuration);

builder.Services.AddRoomMatchInfrastructure(builder.Configuration);
builder.Services.AddHostedService<RoomMappingSupStaticRoomInfoStatisticsWorker>();

await builder.Build().RunAsync();

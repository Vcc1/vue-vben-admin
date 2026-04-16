using DbTool;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

var processModule = Process.GetCurrentProcess().MainModule;
if (processModule is not null)
{
    var pathToContentRoot = Path.GetDirectoryName(processModule.FileName)!;
    Directory.SetCurrentDirectory(pathToContentRoot);
}

var builder = Host.CreateApplicationBuilder(args);
MengTuDBConfigTool.InitDBConfig(builder.Configuration, "RoomMapping");

builder.Services.AddDbContext<RoomMappingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RoomMapping")));
builder.Services.AddHostedService<RoomMappingSupStaticRoomInfoStatisticsWorker>();

await builder.Build().RunAsync();

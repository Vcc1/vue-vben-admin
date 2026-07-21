using ExpediaChaseRoomApp;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using VBase.AppMetrics;
using VBaseFramework.ServiceDefaults;

var processModule = Process.GetCurrentProcess().MainModule;
if (processModule is not null)
{
    var pathToContentRoot = Path.GetDirectoryName(processModule.FileName)!;
    Directory.SetCurrentDirectory(pathToContentRoot);
}

var host = Serve.Run(
    new RunOptions { Args = args, ServiceName = "MT-ExpediaChaseRooms" },
    (services, configuration) =>
    {
        //ChangeToken.OnChange(() => configuration.GetReloadToken(), () =>
        //{
        //    Console.WriteLine("Configuration changed, reloading...");
        //    // You can add any additional logic here if needed when the configuration changes
        //});
  
        services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
        services.AddConnectmateRemoteConfiguration(configuration);
        services
            .AddAppMetrics(configuration)
            .AddExpediaChaseRoomApp(configuration)
            .AddExpediaChaseRoomInfrastructure(configuration);
    });

await host.RunAsync();

using Framework.Core.IOC;
using Infrastructure.MetricsAndAlertsApp;
using Infrastructure.Sql.MetricsAndAlerts;
using MengTu.Connectmate.Client;
//using MengTuConnectmateApi.Filters;
using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using VBaseFramework.ServiceDefaults;

// In framework-dependent/container runs, MainModule points to the dotnet host.
// AppContext.BaseDirectory stays on the app publish directory where appsettings.json lives.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var host = Serve.Run(new RunOptions
{
    Args = args,
    ServiceName = "MT-MetricsAndAlerts",
    EnableWeb = true
}, (services, configuration) =>
{
    services.AddHealthChecks();
    services.AddConnectmateRemoteConfiguration(configuration);
    services.AddSingleton<IConfiguration>(configuration);
    services.Configure<JsonOptions>(options =>
    {
        var json = options.JsonSerializerOptions;
        json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        json.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        json.PropertyNameCaseInsensitive = true;
        json.WriteIndented = true;
        json.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
    
    services
        .AddMetricsAndAlertsApp(configuration)
        .AddMetricsAndAlertsInfrastructure()
        .AddBusiness(new Assembly[]
        {
            typeof(MetricsAndAlertsDbContext).Assembly
        });
});


await host.RunAsync();

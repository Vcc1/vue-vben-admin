using GrafanaAlertApp;
using Infrastructure.GrafanaAlertApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Encodings.Web;
using System.Text.Json;
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
    ServiceName = "MT-GrafanaAlertWebhook",
    EnableWeb = true,
}, (services, configuration) =>
{
    services.AddHealthChecks();
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
        .AddGrafanaAlertApp(configuration)
        .AddGrafanaAlertInfrastructure();
}, (WebApplication app) =>
{
    app.Use(async (context, next) =>
    {
        context.Request.EnableBuffering();
        await next(context);
    });
});

await host.RunAsync();

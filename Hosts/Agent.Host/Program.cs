using AgentPlatform.Api;
using AgentPlatform.App;
using AgentPlatform.Infra;
using HotelResearch.Api;
using HotelResearch.App;
using HotelResearch.Infra;
using HotelBusiness.Api;
using HotelBusiness.App;
using HotelBusiness.Infra;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("MengTu.Connectmate.Client", LogLevel.Warning);
builder.Logging.AddFilter(
    "System.Net.Http.HttpClient.IConnectmateConfigurationPullClient",
    LogLevel.Warning);

builder.Services.AddHttpClient();
builder.Services.AddConnectmateDatabaseSerializer<ConnectmateFallbackSerializer>();
if (!builder.Configuration.GetValue<bool>("Connectmate:SkipRemoteConfiguration"))
{
    builder.Services.AddConnectmateRemoteConfiguration(builder.Configuration);
}

builder.Services.AddAgentPlatformApplication();
builder.Services.AddAgentPlatformInfrastructure(builder.Configuration);
builder.Services.AddHotelBusinessApplication();
builder.Services.AddHotelBusinessInfrastructure(builder.Configuration);
builder.Services.AddHotelResearchApplication();
builder.Services.AddHotelResearchInfrastructure();
builder.Services
    .AddControllers()
    .AddAgentPlatformApi()
    .AddHotelBusinessApi()
    .AddHotelResearchApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

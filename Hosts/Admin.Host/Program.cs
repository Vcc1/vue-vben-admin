using AgentPlatform.Api;
using AgentPlatform.App;
using AgentPlatform.Infra;
using Admin.Host.OpenApi;
using Backoffice.Api;
using Backoffice.App;
using Backoffice.Infra;
using Framework.Core.IOC;
using HotelMatchingPipeline.Api;
using HotelMatchingPipeline.AgentTools;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.Infra;
using Infrastructure.Sql.RoomMapping;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using AgentPlatformOpenApi = AgentPlatform.Api.OpenApiMetadata;
using BackofficeOpenApi = Backoffice.Api.OpenApiMetadata;
using HotelMatchingOpenApi = HotelMatchingPipeline.Api.OpenApiMetadata;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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

builder.Services.AddHotelMatchingServices(builder.Configuration);
builder.Services.AddHotelMatchingInfrastructure(builder.Configuration);
builder.Services.AddAgentPlatformApplication();
builder.Services.AddAgentPlatformInfrastructure(builder.Configuration);
builder.Services.AddHotelMatchingAgentTools();
builder.Services.AddBackofficeApplication();
builder.Services.AddBackofficeInfrastructure();
builder.Services.AddBusiness([typeof(RoomMappingDbContext).Assembly]);
builder.Services
    .AddControllers()
    .AddHotelMatchingApi()
    .AddAgentPlatformApi()
    .AddBackofficeApi();

var openApiDocuments = new[]
{
    new { AgentPlatformOpenApi.DocumentName, AgentPlatformOpenApi.DisplayName },
    new { HotelMatchingOpenApi.DocumentName, HotelMatchingOpenApi.DisplayName },
    new { BackofficeOpenApi.DocumentName, BackofficeOpenApi.DisplayName }
};

foreach (var document in openApiDocuments)
{
    builder.Services.AddOpenApi(document.DocumentName, options =>
    {
        options.ShouldInclude = description => string.Equals(
            description.GroupName,
            document.DocumentName,
            StringComparison.OrdinalIgnoreCase);
        options.AddSchemaTransformer<DescriptionSchemaTransformer>();
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    foreach (var document in openApiDocuments)
    {
        options.SwaggerEndpoint(
            $"/openapi/{document.DocumentName}.json",
            document.DisplayName);
    }
});


app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();

using AgentPlatform.Api;
using AgentPlatform.App;
using AgentPlatform.Infra;
using Admin.Host;
using Admin.Host.OpenApi;
using AdminIdentity.Api;
using AdminIdentity.App;
using AdminIdentity.Infra;
using Auditing.Api;
using Auditing.App;
using Auditing.Infra;
using OidcServer.Api;
using OidcServer.App;
using OidcServer.Infra;
using ApiGateway.Api;
using ApiGateway.App;
using ApiGateway.Infra;
using Backoffice.Api;
using Backoffice.App;
using Backoffice.Infra;
using Framework.Core.IOC;
using HotelMatchingPipeline.Api;
using HotelMatchingPipeline.AgentTools;
using HotelMatchingPipeline.App;
using HotelMatchingPipeline.Infra;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.Admin;
using MengTu.Connectmate.Client;
using MengTu.Connectmate.Client.Tool;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskScheduling.Api;
using TaskScheduling.App;
using TaskScheduling.Infra;
using AgentPlatformOpenApi = AgentPlatform.Api.OpenApiMetadata;
using BackofficeOpenApi = Backoffice.Api.OpenApiMetadata;
using HotelMatchingOpenApi = HotelMatchingPipeline.Api.OpenApiMetadata;
using GatewayOpenApi = ApiGateway.Api.OpenApiMetadata;
using AuditingOpenApi = Auditing.Api.OpenApiMetadata;
using AdminOpenApi = AdminIdentity.Api.OpenApiMetadata;

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

builder.Services.AddAdminDatabase(builder.Configuration, "RoomMapping");
builder.Services.AddAdminIdentityApplication();
builder.Services.AddAdminIdentityInfrastructure(builder.Configuration);
builder.Services.AddAuditingInfrastructure();
builder.Services.AddOidcServerApplication();
builder.Services.AddOidcServerInfrastructure(builder.Configuration);
builder.Services.AddHotelMatchingServices(builder.Configuration);
builder.Services.AddHotelMatchingInfrastructure(builder.Configuration);
builder.Services.AddHostedService<HotelMatchVetoRedisSyncWorker>();
builder.Services.AddAgentPlatformApplication();
builder.Services.AddAgentPlatformInfrastructure(builder.Configuration);
builder.Services.AddHotelMatchingAgentTools();
builder.Services.AddBackofficeApplication();
builder.Services.AddBackofficeInfrastructure();
builder.Services.AddApiGatewayApplication();
builder.Services.AddApiGatewayInfrastructure();
builder.Services.AddBusiness([typeof(RoomMappingDbContext).Assembly]);
builder.Services.AddTaskSchedulingApplication();
builder.Services.AddTaskSchedulingInfrastructure(builder.Configuration);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddProblemDetails();

// 前端本地直连各服务端口（不再走 vite proxy），需允许跨域访问 OIDC 端点
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5101", "http://localhost:5201"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    // "*" 走 SetIsOriginAllowed，因为 AllowCredentials 与 AllowAnyOrigin 互斥
    if (corsOrigins.Contains("*"))
    {
        policy.SetIsOriginAllowed(_ => true);
    }
    else
    {
        policy.WithOrigins(corsOrigins);
    }

    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddExceptionHandler<AdminApiExceptionHandler>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    })
    .AddAdminIdentityApi()
    .AddOidcServerApi()
    .AddTaskSchedulingApi()
    .AddHotelMatchingApi()
    .AddAgentPlatformApi()
    .AddBackofficeApi()
    .AddApiGatewayApi()
    .AddAuditingApi();

var openApiDocuments = new[]
{
    new { AdminOpenApi.DocumentName, AdminOpenApi.DisplayName },
    new { AgentPlatformOpenApi.DocumentName, AgentPlatformOpenApi.DisplayName },
    new { HotelMatchingOpenApi.DocumentName, HotelMatchingOpenApi.DisplayName },
    new { BackofficeOpenApi.DocumentName, BackofficeOpenApi.DisplayName },
    new { GatewayOpenApi.DocumentName, GatewayOpenApi.DisplayName },
    new { AuditingOpenApi.DocumentName, AuditingOpenApi.DisplayName }
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

app.UseExceptionHandler();
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

app.UseCors();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var requiresValidation = context.Request.Path.StartsWithSegments("/api/admin")
        && (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method));
    var isCookie = string.Equals(
        context.User.Identity?.AuthenticationType,
        IdentityConstants.ApplicationScheme,
        StringComparison.Ordinal);
    var isLogin = context.Request.Path.Equals("/api/admin/auth/login", StringComparison.OrdinalIgnoreCase);
    if (requiresValidation && isCookie && !isLogin)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context);
    }
    await next(context);
});

app.UseAuthorization();

app.UseGatewayProxy();

app.MapControllers();

app.Run();

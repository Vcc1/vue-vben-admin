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
using RoomMatch.Api;
using RoomMatch.App;
using RoomMatch.Core;
using RoomMatch.Admin.Api;
using RoomMatch.Admin.App;
using RoomMatch.Admin.Infra;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskScheduling.Api;
using TaskScheduling.App;
using TaskScheduling.Infra;
using Notification.Api;
using Notification.App;
using Notification.Infra;
using AgentPlatformOpenApi = AgentPlatform.Api.OpenApiMetadata;
using BackofficeOpenApi = Backoffice.Api.OpenApiMetadata;
using HotelMatchingOpenApi = HotelMatchingPipeline.Api.OpenApiMetadata;
using GatewayOpenApi = ApiGateway.Api.OpenApiMetadata;
using RoomMatchOpenApi = RoomMatch.Api.OpenApiMetadata;
using RoomMatchAdminOpenApi = RoomMatch.Admin.Api.OpenApiMetadata;
using AuditingOpenApi = Auditing.Api.OpenApiMetadata;
using AdminOpenApi = AdminIdentity.Api.OpenApiMetadata;

using NLog.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Logging.ClearProviders();
builder.Logging.AddNLog("Configs/nlog.config");

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
builder.Services.AddRoomMatchInfrastructure(builder.Configuration);
builder.Services.AddRoomMatchApplication();
builder.Services.AddRoomMatchAdminInfrastructure();
builder.Services.AddRoomMatchAdminApplication();
builder.Services.AddTaskSchedulingApplication();
builder.Services.AddTaskSchedulingInfrastructure(builder.Configuration);
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure();
builder.Services.AddNotificationSignalR();
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
    .AddRoomMatchApi()
    .AddRoomMatchAdminApi()
    .AddAgentPlatformApi()
    .AddBackofficeApi()
    .AddApiGatewayApi()
    .AddAuditingApi()
    .AddNotificationApi();

var openApiDocuments = new[]
{
    new { AdminOpenApi.DocumentName, AdminOpenApi.DisplayName },
    new { AgentPlatformOpenApi.DocumentName, AgentPlatformOpenApi.DisplayName },
    new { HotelMatchingOpenApi.DocumentName, HotelMatchingOpenApi.DisplayName },
    new { RoomMatchOpenApi.DocumentName, RoomMatchOpenApi.DisplayName },
    new { RoomMatchAdminOpenApi.DocumentName, RoomMatchAdminOpenApi.DisplayName },
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

var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:5101";

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "")
    {
        context.Response.Redirect(frontendUrl, permanent: false);
        return;
    }
    await next(context);
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();

app.UseAuthentication();

// OIDC access token 里虽有 sid，但 OpenIddict validation 管道不检查会话状态。
// 此中间件在认证之后立即校验 sid，被吊销的 session 直接返回 401。
app.UseSessionValidation();

// 登录前的匿名端点：调用方身份靠请求体里的凭据 / stepUpToken / refreshToken 自证，不读 cookie。
// 浏览器却可能残留 AS 侧签发的 Identity cookie（OIDC 授权流程、上一次登录都会写入），
// 此时 SmartScheme 在没有 Bearer 头时会回落到 cookie 方案，antiforgery 校验就会把
// 新设备二次验证与令牌续期整条链路挡死，所以这些路径显式豁免。
string[] preAuthPaths =
[
    "/api/admin/auth/login",
    "/api/admin/auth/stepup",
    "/api/admin/auth/refresh",
];

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var requiresValidation = path.StartsWithSegments("/api/admin")
        && (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method));
    var isCookie = string.Equals(
        context.User.Identity?.AuthenticationType,
        IdentityConstants.ApplicationScheme,
        StringComparison.Ordinal);
    var isPreAuth = preAuthPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    if (requiresValidation && isCookie && !isPreAuth)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context);
    }
    await next(context);
});

app.UseAuthorization();

// 等级闸门必须在 UseAuthorization 之后：角色不足先返回 403，已授权的请求才进入会话等级校验。
app.UseSessionLevelEnforcement();

app.UseGatewayProxy();

app.MapControllers();

app.MapNotificationHub();

app.Run();

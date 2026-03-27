
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using VBaseFramework.Api.Core;
using VBaseFramework.Core.NLog;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{

    /// <summary>
    /// 对跨域 Json 权限 SwaggerGen  日志 等组件进行统一配置
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder AddWebServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.AddControllers(opetation =>
        {
            opetation.Filters.Add<BaseExpextFilter>();
        }).AddJsonOptions(options =>
        {
            var json = options.JsonSerializerOptions;
            // 忽略循环引用
            json.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            // 首字母小写（camelCase）
            json.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            // 忽略 null
            json.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            // 时间格式需要自定义 Converter
            json.Converters.Add(new DateTimeConverter());
        });

        //    .AddNewtonsoftJson(options =>
        //{

        //    //忽略循环引用
        //    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        //    //不使用驼峰样式的key
        //    //options.SerializerSettings.ContractResolver = new DefaultContractResolver();
        //    //首字母小写
        //    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        //    options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
        //    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        //});
        builder.Services.AddCors();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "用户登陆状态 通过登陆接口获取",
                Name = "Token",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            options.AddSecurityDefinition("ApiBearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "Api请求Token 测试系统填写任意字符即可 生产系统请填写正式Key  不需要登陆",
                Name = "ApiToken",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "ApiBearer"
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "ApiBearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
            options.SwaggerDoc("OTAHotelRoomMatch", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "房型匹配", Version = "v1" });
            options.SwaggerDoc("OTATool", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "OTA相关工具", Version = "v1" });
            options.SwaggerDoc("System", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "系统API", Version = "v1" });
        });
        //builder.Services.AddSwaggerGen();


        AddServiceDefaults(builder.Services);
        return builder;
    }

    public static void AddServiceDefaults(IServiceCollection services)
    {

        services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();
            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });
        services.AddLogging(m => { m.AddNLog("Configs/nlog.config"); });
        // 注册 Scoped 作用域的日志上下文服务
        services.AddScoped<ILogContextService, LogContextService>();
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        //builder.Logging.AddOpenTelemetry(logging =>
        //{
        //    logging.IncludeFormattedMessage = true;
        //    logging.IncludeScopes = true;
        //});

        //builder.Services.AddOpenTelemetry()
        //    .WithMetrics(metrics =>
        //    {
        //        metrics.AddAspNetCoreInstrumentation()
        //            .AddHttpClientInstrumentation()
        //            .AddRuntimeInstrumentation();
        //    })
        //    .WithTracing(tracing =>
        //    {
        //        tracing.AddAspNetCoreInstrumentation()
        //            // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
        //            //.AddGrpcClientInstrumentation()
        //            .AddHttpClientInstrumentation();
        //    });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["VBase"]);
        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.

        // All health checks must pass for app to be considered ready to accept traffic after starting
        //app.MapHealthChecks("/health", new HealthCheckOptions
        //{
        //    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse // 或自定义ResponseWriter
        //});

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        app.UseCors(builder => builder.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/OTATool/swagger.json", "OTA工具");
            c.SwaggerEndpoint("/swagger/OTAHotelRoomMatch/swagger.json", "房型匹配");
            c.SwaggerEndpoint("/swagger/System/swagger.json", "系统API");
        });

        app.UseMiddleware<LoggingMiddleware>();
        return app;
    }
}
public class AddAuthTokenHeaderParameter : IOperationFilter
{

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters = new List<OpenApiParameter>
         {
             new OpenApiParameter
             {
                 Name = "Token",
                 Description = "Token",
                 In = ParameterLocation.Header,
                 Required = false
             }
         };
    }
}
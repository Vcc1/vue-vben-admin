//using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VBaseFramework.Api.Core;
using VBaseFramework.ServiceDefaults.Core.Auth;
using VBaseFramework.Core.EF.Log;
using VBaseFramework.Core.NLog;

namespace VBaseFramework.ServiceDefaults
{
    public class WebAppHostWrapper : IHost
    {
        private readonly WebApplication _app;

        public WebAppHostWrapper(WebApplication app)
        {
            _app = app;
        }

        public IServiceProvider Services => _app.Services;
        public void Dispose() => _app.DisposeAsync();

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _app.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _app.StopAsync(cancellationToken);
        }
    }

    public static class Serve
    {
        private static string? GetArg(string[] args, string name)
        {
            var key = $"--{name}=";
            return args.FirstOrDefault(a => a.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                       ?.Substring(key.Length);
        }
        public static IHost Run(RunOptions options, Action<IServiceCollection, ConfigurationManager> configService, Action<WebApplication> action = null)
        {
            // 解析参数（保持与你原来一致）
            var serviceName = GetArg(options.Args, "serviceName") ?? options.ServiceName;
            options.EnableWeb = options.EnableWeb ?? options.Args.Contains("--web", StringComparer.OrdinalIgnoreCase);


            if (options.EnableWeb == true)
            {
                // 创建 WebApplication，注册 controller 等
                var builder = WebApplication.CreateBuilder(options.Args);

                builder.Services.AddSwaggerGen(Extensions.AddSwaggerXmlComments);
                // 注册 controller 等 Web 内容
                builder.Services.AddControllers(options =>
                {
                    options.Filters.Add<BaseExpextFilter>();
                    options.Filters.Add(new WrapApiResultAttribute());
                });
                builder.Logging.ClearProviders();
                builder.Logging.AddNLog("Configs/nlog.config");


                // ✅ 自动识别是否在 Windows 服务环境中（控制台启动不会注册）
                if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
                {
                    builder.Services.AddWindowsService(options2 =>
                    {
                        options2.ServiceName = serviceName ?? options.ServiceName;
                    });
                }

                CServices(builder.Services);
                configService(builder.Services, builder.Configuration);
                var app = builder.Build();


                app.UseStaticFiles();
                app.UseRouting();
                if (action != null)
                {
                    action(app);
                }

                app.MapDefaultEndpoints();
                app.UseHttpsRedirection();
                app.UseAuthorization();
                app.MapControllers();
                return new WebAppHostWrapper(app);
            }
            else
            {
                var config = new ConfigurationManager();
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddCommandLine(options.Args)
                      .AddEnvironmentVariables();

                // 不使用 Web，仅启动后台逻辑
                var hostBuilder = Host.CreateDefaultBuilder()
                    .ConfigureHostConfiguration(conf =>
                    {
                        conf.AddConfiguration(config); // ✅ 这步是关键
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();                        // 清除默认
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);         // 设置日志等级
                        logging.AddNLog("Configs/nlog.config");          // 加载 NLog
                    })
                    .ConfigureServices(services =>
                    {
                        CServices(services);
                        configService(services, config);
                    });

                if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
                {
                    hostBuilder.UseWindowsService(options2 =>
                    {
                        options2.ServiceName = serviceName ?? options.ServiceName;
                    });
                }

                return hostBuilder.Build();

            }
        }

        private static void CServices(IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddLogging();
            // 注册 Scoped 作用域的日志上下文服务
            services.AddScoped<EFLoggerProvider, EFLoggerProvider>();
            services.AddScoped<ILogContextService, LogContextService>();
        }
    }





}

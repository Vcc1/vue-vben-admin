using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.NLog
{
    public interface ILogContextService
    {
        IDisposable PushProperty(string key, string value);
    }
    public class LogContextService : ILogContextService
    {
        public IDisposable PushProperty(string key, string value)
        {
            return ScopeContext.PushProperty(key, value);
        }
    }

    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;



        public LoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            context.Request.Cookies.TryGetValue("ClientId", out var clientId);
            if (string.IsNullOrEmpty(clientId))
            {
                clientId = Guid.NewGuid().ToString("N");
                context.Response.Cookies.Append("ClientId", clientId);
            }

            var logContextService = context.RequestServices.GetService<ILogContextService>();
            var log = context.RequestServices.GetService<ILogger<LogContextService>>();
            using (logContextService.PushProperty("RequestId", context.TraceIdentifier))
            using (logContextService.PushProperty("ClientId", clientId))
            using (logContextService.PushProperty("ActionId2", Guid.NewGuid().ToString("N")))
            {
                //log.LogInformation("123");
                await _next(context);
            }
        }
    }
}

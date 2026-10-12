using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Microsoft.Extensions.Caching.Distributed;
using NLog;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace VBaseFramework.Api.Core
{
    public class HttpLogFilter : IAsyncActionFilter
    {
        readonly ILogger<HttpLogFilter> _logger;
        readonly IDistributedCache _cache;
        public HttpLogFilter(IDistributedCache cache, ILogger<HttpLogFilter> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();  // 开始计时

            // 检查 ModelState 是否有效
            if (!context.ModelState.IsValid)
            {
                // 你可以在这里执行自定义逻辑，如记录日志或做其他事情
                Console.WriteLine("模型验证失败");
                // 你可以选择在此返回自定义错误消息，避免框架直接返回 400
                context.Result = new BadRequestObjectResult(context.ModelState);
                _logger.LogError($"{stopwatch.ElapsedMilliseconds} 模型验证失败");
                return; // 停止后续执行
            }

            var description =
                (Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor;

            var Controllername = description.ControllerName.ToLower();
            var Actionname = description.ActionName.ToLower();

            //匿名标识
            var authorize = description.MethodInfo.GetCustomAttribute(typeof(AuthAttribute));
            if (authorize == null)
            {
                await next();

                return;
            }
            var token = "";
            try
            {
                token = context.HttpContext.Request.Headers["token"];
            }
            catch (Exception e)
            {

            }
            var userId = await _cache.GetStringAsync(token);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(await _cache.GetStringAsync(token)))
            {

                context.HttpContext.Response.StatusCode = 200;
                context.Result = new JsonResult(new ErrorResponse
                {
                    Code = 401,
                    Message = "认证失败，请提供认证信息"
                });
                return;
            }
            else
            {
                ScopeContext.PushProperty("UserId", userId);
            }
            await next();
        }
    }
}

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace VBaseFramework.Api.Core
{
    public class BaseExpextFilter : ExceptionFilterAttribute
    {
        private ILogger<BaseExpextFilter> logger;
        public BaseExpextFilter(ILogger<BaseExpextFilter> logger)
        {
            this.logger = logger;
        }
        public override void OnException(ExceptionContext context)
        {
            if (!context.ExceptionHandled) //如果错误没有处理过
            {
                var err = context.Exception as VBaseException;
                if (err != null)
                {
                    context.Result = new JsonResult(ApiResult.Error(err.Code, err.Message))
                    {
                        StatusCode = StatusCodes.Status200OK
                    };
                    context.ExceptionHandled = true; //表示错误已经处理过
                }
                else
                {
                    context.Result = new JsonResult(ApiResult.Error(500, context.Exception.Message))
                    {
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                    logger.LogError(context.Exception.Message);
                    context.ExceptionHandled = true; //表示错误已经处理过
                }


            }
        }
        public override Task OnExceptionAsync(ExceptionContext context)
        {
            this.OnException(context);
            return Task.CompletedTask;
        }
        //header中 x-requested-with  XMLHttpRequest  //表明是AJax异步
        private bool IsAjax(HttpRequest httpRequest)
        {
            string requestType = httpRequest.Headers["x-requested-with"] ;
            return "XMLHttpRequest".Equals(requestType, StringComparison.Ordinal);
        }
    }
}

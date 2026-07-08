using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;

namespace VBaseFramework.Api.Core;

public sealed class ApiResult
{
    public object? Data { get; set; }
    public bool IsSuccess { get; set; }
    public string Msg { get; set; } = "操作成功";
    public int ErrorCode { get; set; }

    public static ApiResult Ok(object? data)
    {
        return new ApiResult
        {
            Data = data,
            IsSuccess = true,
            Msg = "操作成功",
            ErrorCode = 0
        };
    }

    public static ApiResult Error(int errorCode, string message, object? data = null)
    {
        return new ApiResult
        {
            Data = data,
            IsSuccess = false,
            Msg = message,
            ErrorCode = errorCode
        };
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class WrapApiResultAttribute : Attribute, IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (GetStatusCode(context.Result) >= 400)
        {
            await next();
            return;
        }

        if (context.Result is FileResult or ContentResult)
        {
            await next();
            return;
        }

        if ((context.Result is ObjectResult existingObjectResult && existingObjectResult.Value is ApiResult)
            || (context.Result is JsonResult existingJsonResult && existingJsonResult.Value is ApiResult))
        {
            await next();
            return;
        }

        switch (context.Result)
        {
            case ObjectResult resultObject:
                context.Result = new OkObjectResult(ApiResult.Ok(resultObject.Value));
                break;
            case JsonResult resultJson:
                context.Result = new OkObjectResult(ApiResult.Ok(resultJson.Value));
                break;
            case EmptyResult:
                context.Result = new OkObjectResult(ApiResult.Ok(null));
                break;
            case StatusCodeResult statusCodeResult when statusCodeResult.StatusCode == StatusCodes.Status204NoContent:
                context.Result = new OkObjectResult(ApiResult.Ok(null));
                break;
        }

        await next();
    }

    private static int GetStatusCode(IActionResult result)
    {
        return result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => StatusCodes.Status200OK
        };
    }
}

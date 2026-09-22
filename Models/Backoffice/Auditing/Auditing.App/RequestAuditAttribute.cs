using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Auditing.App;

/// <summary>
/// 标注在控制器或 action 上即审计其请求与响应。请求 URL（Path + QueryString）、状态码、耗时、
/// actor 始终记录；请求体与响应体各自有独立长度上限，设为 0 即不记录对应 body。
/// body 经 <see cref="SensitivePathRedactor"/> 脱敏后限长。审计写入失败绝不影响主请求。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequestAuditAttribute : Attribute, IAsyncActionFilter, IAsyncAlwaysRunResultFilter
{
    private const string StateKey = "__auditing_request_state";

    private static readonly JsonSerializerOptions FallbackJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>请求体最大保留字符数，超出截断并标记。0 = 不记录请求体。</summary>
    public int MaxRequestBodyLength { get; set; } = 4096;

    /// <summary>响应体最大保留字符数，超出截断并标记。0 = 不记录响应体。</summary>
    public int MaxResponseBodyLength { get; set; } = 4096;

    /// <summary>需脱敏的敏感字段 path。无内置默认词——未声明的字段会原样记录，处理密码/密钥的接口务必显式声明。</summary>
    public string[] SensitivePaths { get; set; } = [];

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var state = new AuditState { StartTimestamp = Stopwatch.GetTimestamp() };

        if (MaxRequestBodyLength > 0)
        {
            var arguments = new Dictionary<string, object?>(context.ActionArguments.Count);
            foreach (var (key, value) in context.ActionArguments)
            {
                if (value is not CancellationToken)
                {
                    arguments[key] = value;
                }
            }

            state.RequestBody = Serialize(context.HttpContext, arguments, MaxRequestBodyLength, out var truncated);
            state.RequestTruncated = truncated;
        }

        context.HttpContext.Items[StateKey] = state;
        return next();
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        await next();

        var http = context.HttpContext;
        if (http.Items.TryGetValue(StateKey, out var raw) && raw is AuditState state)
        {
            http.Items.Remove(StateKey);
            await PersistAsync(context, state);
        }
    }

    private async Task PersistAsync(ResultExecutingContext context, AuditState state)
    {
        var http = context.HttpContext;
        var store = http.RequestServices.GetService<IRequestAuditStore>();
        if (store is null)
        {
            return;
        }

        string? responseBody = null;
        var responseTruncated = false;
        if (MaxResponseBodyLength > 0 && context.Result is ObjectResult { Value: not null } objectResult)
        {
            responseBody = Serialize(http, objectResult.Value, MaxResponseBodyLength, out responseTruncated);
        }

        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var duration = Stopwatch.GetElapsedTime(state.StartTimestamp);

        var entry = new RequestAuditEntry(
            http.Request.Method,
            http.Request.Path.Value ?? string.Empty,
            Truncate(http.Request.QueryString.Value, 1000),
            descriptor?.ControllerName,
            descriptor?.ActionName,
            http.Response.StatusCode,
            duration.Milliseconds,
            state.RequestBody,
            state.RequestTruncated,
            responseBody,
            responseTruncated);

        try
        {
            // 用 None：客户端断开时 RequestAborted 已取消，但审计仍应落库。
            await store.WriteAsync(entry, CancellationToken.None);
        }
        catch
        {
            // 审计是旁路关注点，写入失败不能影响业务响应。
        }
    }

    private string? Serialize(HttpContext http, object? value, int maxLength, out bool truncated)
    {
        truncated = false;
        if (value is null)
        {
            return null;
        }

        string json;
        try
        {
            var options = http.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.JsonSerializerOptions;
            json = options is not null
                ? JsonSerializer.Serialize(value, options)
                : JsonSerializer.Serialize(value, FallbackJsonOptions);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or JsonException)
        {
            return null;
        }

        var (redacted, wasTruncated) = SensitivePathRedactor.RedactAndCap(json, SensitivePaths, maxLength);
        truncated = wasTruncated;
        return redacted;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;

    private sealed class AuditState
    {
        public long StartTimestamp { get; init; }
        public string? RequestBody { get; set; }
        public bool RequestTruncated { get; set; }
    }
}

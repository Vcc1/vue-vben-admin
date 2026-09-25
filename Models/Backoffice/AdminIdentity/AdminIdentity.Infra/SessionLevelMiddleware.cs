using System.Security.Claims;
using System.Text.Json;
using AdminIdentity.App;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdminIdentity.Infra;

/// <summary>
/// 认证事件把每请求算出的会话等级放进 HttpContext.Items，等级中间件直接复用，避免重复查库。
/// </summary>
public static class SessionContext
{
    private const string LevelKey = "__mengtu_session_level";
    private const string SidKey = "__mengtu_session_sid";

    public static void Set(HttpContext context, string? sid, int? level)
    {
        context.Items[SidKey] = sid;
        context.Items[LevelKey] = level;
    }

    public static bool TryGetLevel(HttpContext context, out int level)
    {
        if (context.Items[LevelKey] is int cached)
        {
            level = cached;
            return true;
        }
        level = 0;
        return false;
    }

    public static string? GetSid(HttpContext context) =>
        context.Items[SidKey] as string ?? context.User.FindFirstValue(SessionClaims.Sid);
}

public static class SessionLevelMiddlewareExtensions
{
    /// <summary>
    /// 必须注册在 UseAuthorization 之后：角色/策略不足时先返回 403，
    /// 只有已通过常规授权的请求才会被等级闸门拦下并返回 401 stepup_required。
    /// </summary>
    public static IApplicationBuilder UseSessionLevelEnforcement(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<SessionLevelMiddleware>();
    }
}

internal sealed class SessionLevelMiddleware(RequestDelegate next, ILogger<SessionLevelMiddleware> logger)
{
    /// <summary>401 响应体的 error 码，前端据此弹出 step-up 对话框而不是跳登录页。</summary>
    public const string StepUpRequired = "stepup_required";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context, ISessionService sessions)
    {
        var required = context.GetEndpoint()?.Metadata.GetMetadata<RequireSessionLevelAttribute>()?.Level;
        if (required is null)
        {
            await next(context);
            return;
        }

        var sid = SessionContext.GetSid(context);

        // API Key 等服务间认证没有用户会话，不适用会话等级模型。
        if (sid is null)
        {
            if (!IsUserSession(context))
            {
                await next(context);
                return;
            }

            await RejectAsync(context, 0, required.Value);
            return;
        }

        var level = SessionContext.TryGetLevel(context, out var cached)
            ? cached
            : await sessions.ValidateAndTouchAsync(sid, context.RequestAborted) ?? 0;

        if (level >= required.Value)
        {
            await next(context);
            return;
        }

        logger.LogInformation(
            "会话 {Sid} 等级 {Level} 不满足 {Path} 要求的 L{Required}，返回 stepup_required。",
            sid, level, context.Request.Path, required.Value);
        await RejectAsync(context, level, required.Value);
    }

    private static bool IsUserSession(HttpContext context)
    {
        var identity = context.User.Identity;
        if (identity?.IsAuthenticated != true)
        {
            return false;
        }
        return !string.Equals(identity.AuthenticationType, ApiKeyAuthenticationHandler.SchemeName, StringComparison.Ordinal);
    }

    private static Task RejectAsync(HttpContext context, int currentLevel, int requiredLevel)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        // 与 AdminApiExceptionHandler 保持同一种响应体（ProblemDetails + error 扩展），
        // 前端只需一条解析路径就能区分"需要 step-up"与其它 401。
        var problem = new ProblemDetails
        {
            Title = "Step-up required",
            Detail = $"该操作需要 L{requiredLevel} 会话等级，当前为 L{currentLevel}。",
            Status = StatusCodes.Status401Unauthorized,
            Instance = context.Request.Path
        };
        problem.Extensions["error"] = StepUpRequired;
        problem.Extensions["currentLevel"] = currentLevel;
        problem.Extensions["requiredLevel"] = requiredLevel;
        // password 提升到 L2；强认证通道提升到 L3。前端据此选择弹窗形态。
        problem.Extensions["channels"] = requiredLevel >= SessionLevels.L3
            ? new[] { StepUpChannels.EmailOtp }
            : new[] { StepUpChannels.Password, StepUpChannels.EmailOtp };

        return context.Response.WriteAsJsonAsync(problem, JsonOptions);
    }
}

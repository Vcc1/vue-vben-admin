using AdminIdentity.App;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace OidcServer.Infra;

public static class SessionValidationMiddlewareExtensions
{
    /// <summary>
    /// 必须在 UseAuthentication 之后注册。
    /// 每次请求都检查 sid 对应的会话是否仍然有效，被吊销的 session 立即返回 401。
    /// </summary>
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<SessionValidationMiddleware>();
    }
}

internal sealed class SessionValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sid = context.User.FindFirst(SessionClaims.Sid)?.Value;
            if (!string.IsNullOrWhiteSpace(sid))
            {
                var sessions = context.RequestServices.GetRequiredService<ISessionService>();
                var level = await sessions.ValidateAndTouchAsync(sid, context.RequestAborted);
                if (level is null)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
        }

        await next(context);
    }
}

using System.Text.Json;
using AdminIdentity.App;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Admin.Host;

internal sealed class AdminApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AdminApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api/admin")) return false;

        var (status, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            StepUpException => (StatusCodes.Status400BadRequest, "Step-up verification failed"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Not authenticated"),
            ArgumentException or JsonException or AntiforgeryValidationException =>
                (StatusCodes.Status400BadRequest, "Invalid request"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Request conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled Admin API exception. TraceId={TraceId}", httpContext.TraceIdentifier);
        else
            logger.LogWarning(exception, "Admin API request rejected. StatusCode={StatusCode} TraceId={TraceId}", status, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? null : exception.Message,
            Instance = httpContext.Request.Path
        };

        // error 码让前端区分"验证码错误"（可重输）与"挑战过期/已使用"（必须回登录页）。
        if (exception is StepUpException stepUp)
        {
            problemDetails.Extensions["error"] = stepUp.Code;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}

using System.Text.Json;
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
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= 500 ? null : exception.Message,
                Instance = httpContext.Request.Path
            }
        });
    }
}

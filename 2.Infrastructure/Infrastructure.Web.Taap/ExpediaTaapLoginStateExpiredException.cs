using System.Net;

namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapLoginStateExpiredException : Exception
{
    public ExpediaTaapLoginStateExpiredException(
        string message,
        HttpStatusCode? statusCode = null,
        string? responseContent = null,
        bool shouldRefreshSharedSha256Hash = false)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
        ShouldRefreshSharedSha256Hash = shouldRefreshSharedSha256Hash;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseContent { get; }

    public bool ShouldRefreshSharedSha256Hash { get; }
}

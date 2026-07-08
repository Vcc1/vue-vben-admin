using System.Net;

namespace ExpediaChaseRoomApp;

public interface IExpediaTaapClient
{
    Task<ExpediaTaapPriceQueryResult> QueryPricesAsync(
        int accountId,
        ExpediaTaapLoginState loginState,
        ExpediaTaapPriceQueryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IExpediaTaapAuthenticationService
{
    Task<ExpediaTaapLoginState> LoginAsync(
        ExpediaTaapCredential credential,
        bool captureSharedSha256Hash = false,
        CancellationToken cancellationToken = default);

    Task<ExpediaTaapLoginState> RefreshSharedSha256HashAsync(
        ExpediaTaapCredential credential,
        ExpediaTaapLoginState? existingLoginState = null,
        CancellationToken cancellationToken = default);
}

public sealed class ExpediaTaapCredential
{
    public required string SigninEmailAddress { get; init; }

    public required string SigninPassword { get; init; }
}

public sealed class ExpediaTaapLoginState
{
    public required string CookieHeader { get; init; }

    public required string Sha256Hash { get; init; }

    public required string ClientInfoToken { get; init; }

    public required string Duaid { get; init; }

    public required int Eapid { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class ExpediaTaapPriceQueryRequest
{
    public required string PropertyId { get; init; }

    public required DateOnly CheckIn { get; init; }

    public required DateOnly CheckOut { get; init; }

    public int Adults { get; init; } = 2;

    public int Rooms { get; init; } = 1;

    public IReadOnlyList<ExpediaTaapRoomOccupancy>? RoomOccupancies { get; init; }

    public string? Sha256Hash { get; init; }

    public string? Locale { get; init; }

    public string? Currency { get; init; }
}

public sealed class ExpediaTaapRoomOccupancy
{
    public int Adults { get; init; }

    public IReadOnlyList<int> ChildrenAges { get; init; } = Array.Empty<int>();
}

public sealed class ExpediaTaapPriceQueryResult
{
    public required string PropertyId { get; init; }

    public required DateOnly CheckIn { get; init; }

    public required DateOnly CheckOut { get; init; }

    public required string RawContent { get; init; }

    public required string RequestContent { get; init; }
}

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

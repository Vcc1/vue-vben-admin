namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapLoginState
{
    public required string CookieHeader { get; init; }

    public required string Sha256Hash { get; init; }

    public required string ClientInfoToken { get; init; }

    public required string Duaid { get; init; }

    public required int Eapid { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

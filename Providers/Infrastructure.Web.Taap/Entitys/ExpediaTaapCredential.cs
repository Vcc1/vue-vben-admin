namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapCredential
{
    public required string SigninEmailAddress { get; init; }

    public required string SigninPassword { get; init; }
}

namespace Infrastructure.Web.Taap;

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

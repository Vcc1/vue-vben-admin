using Infrastructure.Web.Taap;

namespace ExpediaChaseRoomApp;

public interface IExpediaTaapAccountSessionService
{
    Task<ExpediaTaapLoginState> GetLoginStateAsync(
        ExpediaTaapAccountWorkerContext account,
        CancellationToken cancellationToken = default);

    Task<ExpediaTaapLoginState> RefreshLoginStateAsync(
        ExpediaTaapAccountWorkerContext account,
        CancellationToken cancellationToken = default);

    Task<string> RefreshSharedSha256HashAsync(CancellationToken cancellationToken = default);

    Task InvalidateAsync(int accountId, CancellationToken cancellationToken = default);
}

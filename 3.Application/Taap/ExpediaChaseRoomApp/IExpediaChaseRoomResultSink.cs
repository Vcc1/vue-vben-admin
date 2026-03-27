using Infrastructure.Web.Taap;

namespace ExpediaChaseRoomApp;

public interface IExpediaChaseRoomResultSink
{
    Task OnSuccessAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaChaseRoomTask task,
        ExpediaTaapPriceQueryResult result,
        CancellationToken cancellationToken = default);

    Task OnErrorAsync(
        ExpediaTaapAccountWorkerContext account,
        ExpediaChaseRoomTask task,
        Exception exception,
        CancellationToken cancellationToken = default);
}

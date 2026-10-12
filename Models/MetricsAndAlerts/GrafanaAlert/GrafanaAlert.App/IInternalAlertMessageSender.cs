namespace GrafanaAlertApp;

public interface IInternalAlertMessageSender
{
    Task SendAsync(InternalAlertMessage message, CancellationToken cancellationToken = default);
}

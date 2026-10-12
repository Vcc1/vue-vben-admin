namespace GrafanaAlertApp;

public interface IGrafanaAlertForwarder
{
    Task ForwardAsync(GrafanaWebhookPayload payload, CancellationToken cancellationToken = default);
}

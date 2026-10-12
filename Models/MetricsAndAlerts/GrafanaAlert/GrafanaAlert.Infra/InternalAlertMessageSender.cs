using GrafanaAlertApp;
using Infrastructure.HTTP.WorkWechatAlert;

namespace Infrastructure.GrafanaAlertApp;

public sealed class InternalAlertMessageSender : IInternalAlertMessageSender
{
    readonly IWorkWechatTemplateMessageApi _api;

    public InternalAlertMessageSender(IWorkWechatTemplateMessageApi api)
    {
        _api = api;
    }

    public Task SendAsync(InternalAlertMessage message, CancellationToken cancellationToken = default)
    {
        return _api.SendAsync(new WorkWechatTemplateMessageRequest
        {
            ToUsers = message.ToUsers,
            Agentid = message.AgentId,
            ToDept = null,
            ToTag = null,
            Urls = null,
            Content = message.Content
        }, cancellationToken);
    }
}

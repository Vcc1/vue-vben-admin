namespace Infrastructure.HTTP.WorkWechatAlert;

public interface IWorkWechatTemplateMessageApi
{
    Task SendAsync(WorkWechatTemplateMessageRequest request, CancellationToken cancellationToken = default);
}

using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.HTTP.WorkWechatAlert;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkWechatAlertHttpProvider(this IServiceCollection services)
    {
        services.AddHttpClient(WorkWechatTemplateMessageApi.HttpClientName);
        services.AddSingleton<IWorkWechatTemplateMessageApi, WorkWechatTemplateMessageApi>();
        return services;
    }
}

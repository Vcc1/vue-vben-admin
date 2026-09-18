using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddApiGatewayApi(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddApplicationPart(typeof(OpenApiMetadata).Assembly);
        return builder;
    }
}

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace Auditing.Api;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddAuditingApi(this IMvcBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddApplicationPart(typeof(OpenApiMetadata).Assembly);
        return builder;
    }
}

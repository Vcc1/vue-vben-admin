using Auditing.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Auditing.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuditingInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISecurityEventLogger, SecurityEventLogger>();
        services.AddScoped<IRequestAuditStore, RequestAuditStore>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        return services;
    }
}

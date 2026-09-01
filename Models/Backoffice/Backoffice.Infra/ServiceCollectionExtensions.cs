using Backoffice.App.HotelPriceComparison;
using Backoffice.Infra.HotelPriceComparison;
using Framework.Core.IOC;
using Infrastructure.Sql.MengTuYaoChi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VBaseFramework.Core.EF.Log;

namespace Backoffice.Infra;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackofficeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<EFLoggerProvider>();
        services.AddBusiness([typeof(MengtuYaoChiDbContext).Assembly]);
        services.AddScoped<IHotelPriceComparisonRunHistoryRepository, MengtuYaoChiRunHistoryRepository>();
        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sql.Admin;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "RoomMapping")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<AdminDbContext>((_, options) =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{connectionStringName}' was not found.");
            options.UseSqlServer(connectionString);
        });
        return services;
    }
}

using AdminIdentity.App;
using Infrastructure.Sql.Admin.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdminIdentity.Infra;

internal sealed class AdminIdentityBootstrapService(
    IServiceScopeFactory scopeFactory,
    IOptions<AdminIdentityOptions> options,
    TimeProvider timeProvider,
    ILogger<AdminIdentityBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AdminRole>>();
        foreach (var roleName in AdminRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new AdminRole { Id = Guid.CreateVersion7(), Name = roleName });
                EnsureSucceeded(result);
            }
        }

        var bootstrap = options.Value.Bootstrap;
        if (string.IsNullOrWhiteSpace(bootstrap.UserName) || string.IsNullOrWhiteSpace(bootstrap.Password))
        {
            logger.LogWarning("No bootstrap administrator is configured. Set AdminIdentity:Bootstrap through secrets when the user table is empty.");
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var displayName = string.IsNullOrWhiteSpace(bootstrap.DisplayName) ? bootstrap.UserName.Trim() : bootstrap.DisplayName.Trim();
        var user = new AdminUser
        {
            Id = Guid.CreateVersion7(),
            UserName = bootstrap.UserName.Trim(),
            DisplayName = displayName,
            Name = displayName,
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        EnsureSucceeded(await userManager.CreateAsync(user, bootstrap.Password));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, AdminRoles.Admin));
        logger.LogInformation("Created bootstrap Admin.Host administrator {UserName}.", user.UserName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
}

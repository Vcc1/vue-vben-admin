using System.Text.Json;
using Infrastructure.Sql.Admin;
using Infrastructure.Sql.Admin.Oidc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OidcServer.Infra;

internal sealed class OidcServerBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<OidcServerBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        await EnsureSchemaAsync(db, cancellationToken);
        await EnsureScopesAsync(db, cancellationToken);
        await EnsureClientsAsync(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureSchemaAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'DisplayNames') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Applications] ADD [DisplayNames] nvarchar(4000) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'JsonWebKeySet') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Applications] ADD [JsonWebKeySet] nvarchar(4000) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'Settings') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Applications] ADD [Settings] nvarchar(4000) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Scopes', N'Descriptions') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Scopes] ADD [Descriptions] nvarchar(4000) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Scopes', N'DisplayNames') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Scopes] ADD [DisplayNames] nvarchar(4000) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Authorizations', N'ConcurrencyToken') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Authorizations] ADD [ConcurrencyToken] nvarchar(50) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Authorizations', N'Type') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Authorizations] ADD [Type] nvarchar(50) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'BackChannelLogoutUri') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Applications] ADD [BackChannelLogoutUri] nvarchar(400) NULL",
            cancellationToken);

        await db.Database.ExecuteSqlAsync(
            $"IF COL_LENGTH(N'dbo.IdentityOIDC_Applications', N'RequiredSessionLevel') IS NULL ALTER TABLE [dbo].[IdentityOIDC_Applications] ADD [RequiredSessionLevel] int NOT NULL CONSTRAINT [DF_IdentityOIDC_Applications_Level] DEFAULT(1)",
            cancellationToken);

        logger.LogInformation("OIDC schema check completed.");
    }

    private async Task EnsureScopesAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        var standardScopes = new (string Name, string DisplayName)[]
        {
            ("openid", "OpenID"),
            ("profile", "User profile"),
            ("email", "Email address"),
        };

        foreach (var (name, displayName) in standardScopes)
        {
            if (await db.OidcScopes.AnyAsync(s => s.Name == name, cancellationToken))
                continue;

            db.OidcScopes.Add(new OidcScope
            {
                Name = name,
                DisplayName = displayName,
                Description = displayName,
                Resources = "[]",
                Properties = "{}",
            });

            logger.LogInformation("Created OIDC scope {Scope}.", name);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureClientsAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        var permissions = new List<string>
        {
            "ept:authorization",
            "ept:token",
            "gt:authorization_code",
            "gt:refresh_token",
            "rst:code",
            "scp:openid",
            "scp:profile",
            "scp:email",
        };

        var existing = await db.OidcApplications
            .FirstOrDefaultAsync(a => a.ClientId == "paycardhub", cancellationToken);

        // 已存在则完全不碰：回调地址等由后台管理界面维护，启动代码不覆盖数据库已有配置。
        if (existing is not null)
            return;

        db.OidcApplications.Add(new OidcApplication
        {
            ClientId = "paycardhub",
            DisplayName = "PayCardHub Admin",
            ClientType = "public",
            ConsentType = "simple",
            RedirectUris = JsonSerializer.Serialize(new[] { "http://localhost:5201/auth/callback" }),
            PostLogoutRedirectUris = JsonSerializer.Serialize(new[] { "http://localhost:5201" }),
            Permissions = JsonSerializer.Serialize(permissions),
            Requirements = "[]",
            Properties = "{}",
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created OIDC client 'paycardhub'.");
    }
}

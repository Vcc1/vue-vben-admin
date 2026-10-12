using AdminIdentity.App;
using Infrastructure.Sql.Admin;
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
    /// <summary>
    /// 会话与设备信任表的幂等 DDL，与 <c>SqlScripts/Identity_Sessions_Tables.Init.sql</c> 同源：
    /// 脚本用于全新库的标准初始化，这里保证已部署环境升级时不会因为漏跑脚本而让登录整条链路 500。
    /// 分批执行：新加的列必须在下一个批次里才能被 CREATE INDEX 引用，否则编译期就报"列名无效"。
    /// </summary>
    private static readonly string[] SessionSchemaDdl =
    [
        """
        IF OBJECT_ID(N'dbo.Identity_Sessions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Identity_Sessions
            (
                Sid varchar(64) NOT NULL CONSTRAINT PK_Identity_Sessions PRIMARY KEY,
                UserId uniqueidentifier NOT NULL,
                DeviceId nvarchar(64) NULL,
                DeviceInfoJson nvarchar(max) NULL,
                LoginMethod varchar(20) NOT NULL CONSTRAINT DF_Identity_Sessions_LoginMethod DEFAULT('pwd'),
                PasswordVerifiedAtUtc datetime2(3) NULL,
                StrongAuthAtUtc datetime2(3) NULL,
                StrongAuthMethod varchar(20) NULL,
                ClientIdsJson nvarchar(1000) NULL,
                CreatedByIp nvarchar(64) NULL,
                UserAgent nvarchar(512) NULL,
                CreatedAtUtc datetime2(3) NOT NULL,
                LastActivityAtUtc datetime2(3) NOT NULL,
                ExpiresAtUtc datetime2(3) NOT NULL,
                RevokedAtUtc datetime2(3) NULL,
                RevokedReason nvarchar(200) NULL,
                RevokedByActorId nvarchar(100) NULL,
                RevokedByActorName nvarchar(100) NULL
            );
            CREATE INDEX IX_Identity_Sessions_User_Revoked
                ON dbo.Identity_Sessions(UserId, RevokedAtUtc);
            CREATE INDEX IX_Identity_Sessions_DeviceId
                ON dbo.Identity_Sessions(DeviceId) WHERE DeviceId IS NOT NULL;
            CREATE INDEX IX_Identity_Sessions_LastActivity
                ON dbo.Identity_Sessions(LastActivityAtUtc);
        END;

        IF OBJECT_ID(N'dbo.Identity_TrustedDevices', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Identity_TrustedDevices
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_Identity_TrustedDevices PRIMARY KEY,
                UserId uniqueidentifier NOT NULL,
                DeviceId nvarchar(64) NOT NULL,
                DeviceInfoJson nvarchar(max) NULL,
                FirstSeenUtc datetime2(3) NOT NULL,
                LastSeenUtc datetime2(3) NOT NULL,
                LastLoginSid varchar(64) NULL,
                Status varchar(20) NOT NULL CONSTRAINT DF_Identity_TrustedDevices_Status DEFAULT('trusted'),
                RevokedReason nvarchar(200) NULL,
                RevokedAtUtc datetime2(3) NULL,
                RevokedByActorId nvarchar(100) NULL,
                CONSTRAINT FK_Identity_TrustedDevices_Users FOREIGN KEY(UserId)
                    REFERENCES dbo.Identity_Users(Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX UX_Identity_TrustedDevices_User_Device
                ON dbo.Identity_TrustedDevices(UserId, DeviceId);
        END;

        IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'Sid') IS NULL
            ALTER TABLE dbo.Identity_RefreshTokens ADD Sid varchar(64) NULL;

        IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'DeviceId') IS NULL
            ALTER TABLE dbo.Identity_RefreshTokens ADD DeviceId nvarchar(64) NULL;

        IF COL_LENGTH(N'dbo.Identity_RefreshTokens', N'UserAgent') IS NULL
            ALTER TABLE dbo.Identity_RefreshTokens ADD UserAgent nvarchar(512) NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Identity_RefreshTokens_Sid'
                       AND object_id = OBJECT_ID(N'dbo.Identity_RefreshTokens'))
            CREATE INDEX IX_Identity_RefreshTokens_Sid
                ON dbo.Identity_RefreshTokens(Sid) WHERE Sid IS NOT NULL;
        """,
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        foreach (var batch in SessionSchemaDdl)
        {
            await db.Database.ExecuteSqlRawAsync(batch, cancellationToken);
        }
        logger.LogInformation("AdminIdentity session schema check completed.");

        WarnAboutUnreachableTrustLevel();

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

    /// <summary>
    /// StepUp 关闭时强认证走不到，L3 是不可达等级；此时若仍要求 L3 才记住设备，
    /// 结果就是任何设备永远不受信、每次登录都重新判定。这是个静默的配置错误，启动时必须喊出来。
    /// </summary>
    private void WarnAboutUnreachableTrustLevel()
    {
        if (options.Value.StepUp.Enabled
            || SessionLevels.Clamp(options.Value.Session.TrustDeviceMinLevel) < SessionLevels.L3)
        {
            return;
        }

        logger.LogWarning(
            "AdminIdentity:Session:TrustDeviceMinLevel 要求 L{Level}，但 AdminIdentity:StepUp:Enabled=false 时强认证不可达，" +
            "任何设备都不会被记为受信。请开启 StepUp，或把 TrustDeviceMinLevel 降到 2。",
            SessionLevels.L3);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
}

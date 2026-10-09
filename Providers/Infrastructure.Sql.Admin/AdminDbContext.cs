using ApiGateway.Domain;
using Infrastructure.Sql.Admin.Auditing;
using Infrastructure.Sql.Admin.Identity;
using Infrastructure.Sql.Admin.Notification;
using Infrastructure.Sql.Admin.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Sql.Admin.Oidc;

namespace Infrastructure.Sql.Admin;

public sealed class AdminDbContext(
    DbContextOptions<AdminDbContext> options)
    : IdentityDbContext<AdminUser, AdminRole, Guid>(options)
{
    public DbSet<AdminApiKey> ApiKeys => Set<AdminApiKey>();
    public DbSet<AdminRefreshToken> RefreshTokens => Set<AdminRefreshToken>();
    public DbSet<AdminSession> Sessions => Set<AdminSession>();
    public DbSet<AdminTrustedDevice> TrustedDevices => Set<AdminTrustedDevice>();
    public DbSet<AdminUserAuditLog> UserAuditLogs => Set<AdminUserAuditLog>();
    public DbSet<AdminPermission> Permissions => Set<AdminPermission>();
    public DbSet<AdminRolePermission> RolePermissions => Set<AdminRolePermission>();
    public DbSet<ScheduledTaskEntity> ScheduledTasks => Set<ScheduledTaskEntity>();
    public DbSet<ScheduledTaskRunEntity> ScheduledTaskRuns => Set<ScheduledTaskRunEntity>();
    public DbSet<ScheduledTaskAuditEntity> ScheduledTaskAudits => Set<ScheduledTaskAuditEntity>();
    public DbSet<GatewayApi> GatewayApis => Set<GatewayApi>();
    public DbSet<GatewayModule> GatewayModules => Set<GatewayModule>();
    public DbSet<GatewayEndpoint> GatewayEndpoints => Set<GatewayEndpoint>();
    public DbSet<GatewayPermission> GatewayPermissions => Set<GatewayPermission>();
    public DbSet<GatewayAuditLog> GatewayAuditLogs => Set<GatewayAuditLog>();
    public DbSet<OidcApplication> OidcApplications => Set<OidcApplication>();
    public DbSet<OidcScope> OidcScopes => Set<OidcScope>();
    public DbSet<OidcAuthorization> OidcAuthorizations => Set<OidcAuthorization>();
    public DbSet<OidcToken> OidcTokens => Set<OidcToken>();
    public DbSet<SecurityEventLog> SecurityEvents => Set<SecurityEventLog>();
    public DbSet<RequestAuditLog> RequestAuditLogs => Set<RequestAuditLog>();
    public DbSet<NotificationTypeEntity> NotificationTypes => Set<NotificationTypeEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationRecipientEntity> NotificationRecipients => Set<NotificationRecipientEntity>();
    public DbSet<UserNotificationPreferenceEntity> UserNotificationPreferences => Set<UserNotificationPreferenceEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureScheduling(builder);
        ConfigureNotification(builder);
        ConfigureOidc(builder);
        builder.ConfigureGateway();
        builder.ConfigureAuditing();
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("Identity_Users", "dbo");
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.UpdatedAtUtc).HasPrecision(3);
            entity.Property(x => x.LastLoginAtUtc).HasPrecision(3);
        });
        builder.Entity<AdminRole>().ToTable("Identity_Roles", "dbo");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("Identity_UserRoles", "dbo");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("Identity_UserClaims", "dbo");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("Identity_UserLogins", "dbo");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("Identity_RoleClaims", "dbo");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("Identity_UserTokens", "dbo");

        builder.Entity<AdminApiKey>(entity =>
        {
            entity.ToTable("Identity_ApiKeys", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Prefix).HasMaxLength(24).IsRequired();
            entity.Property(x => x.SecretHash).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(3);
            entity.Property(x => x.LastUsedAtUtc).HasPrecision(3);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(3);
            entity.HasIndex(x => x.Prefix).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AdminRefreshToken>(entity =>
        {
            entity.ToTable("Identity_RefreshTokens", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.CreatedByIp).HasMaxLength(64);
            entity.Property(x => x.Sid).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.DeviceId).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(3);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(3);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.Sid);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdminSession>(entity =>
        {
            entity.ToTable("Identity_Sessions", "dbo");
            entity.HasKey(x => x.Sid);
            entity.Property(x => x.Sid).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.DeviceId).HasMaxLength(64);
            entity.Property(x => x.LoginMethod).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.StrongAuthMethod).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.RevokedReason).HasMaxLength(200);
            entity.Property(x => x.RevokedByActorId).HasMaxLength(100);
            entity.Property(x => x.RevokedByActorName).HasMaxLength(100);
            entity.Property(x => x.CreatedByIp).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.PasswordVerifiedAtUtc).HasPrecision(3);
            entity.Property(x => x.StrongAuthAtUtc).HasPrecision(3);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.LastActivityAtUtc).HasPrecision(3);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(3);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
            entity.HasIndex(x => x.DeviceId);
            entity.HasIndex(x => x.LastActivityAtUtc);
        });

        builder.Entity<AdminTrustedDevice>(entity =>
        {
            entity.ToTable("Identity_TrustedDevices", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.LastLoginSid).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.RevokedReason).HasMaxLength(200);
            entity.Property(x => x.RevokedByActorId).HasMaxLength(100);
            entity.Property(x => x.FirstSeenUtc).HasPrecision(3);
            entity.Property(x => x.LastSeenUtc).HasPrecision(3);
            entity.Property(x => x.RevokedAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.UserId, x.DeviceId }).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdminUserAuditLog>(entity =>
        {
            entity.ToTable("Identity_UserAuditLogs", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AuthenticationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.TargetUserId, x.OccurredAtUtc });
        });

        builder.Entity<AdminPermission>(entity =>
        {
            entity.ToTable("Identity_Permissions", "dbo");
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.UpdatedAtUtc).HasPrecision(3);
        });

        builder.Entity<AdminRolePermission>(entity =>
        {
            entity.ToTable("Identity_RolePermissions", "dbo");
            entity.HasKey(x => new { x.RoleName, x.PermissionCode });
            entity.Property(x => x.RoleName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PermissionCode).HasMaxLength(100).IsRequired();
        });
    }

    private static void ConfigureOidc(ModelBuilder builder)
    {
        builder.Entity<OidcApplication>(entity =>
        {
            entity.ToTable("IdentityOIDC_Applications", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ClientSecret).HasMaxLength(400);
            entity.Property(x => x.ClientType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ApplicationType).HasMaxLength(50);
            entity.Property(x => x.ConsentType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.RedirectUris).HasMaxLength(4000);
            entity.Property(x => x.PostLogoutRedirectUris).HasMaxLength(4000);
            entity.Property(x => x.Permissions).HasMaxLength(4000);
            entity.Property(x => x.Requirements).HasMaxLength(400);
            entity.Property(x => x.Properties).HasMaxLength(4000);
            entity.Property(x => x.BackChannelLogoutUri).HasMaxLength(400);
            entity.Property(x => x.ConcurrencyToken).HasMaxLength(50).IsConcurrencyToken();
            entity.HasIndex(x => x.ClientId).IsUnique();
        });

        builder.Entity<OidcScope>(entity =>
        {
            entity.ToTable("IdentityOIDC_Scopes", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Resources).HasMaxLength(4000);
            entity.Property(x => x.Properties).HasMaxLength(4000);
            entity.Property(x => x.ConcurrencyToken).HasMaxLength(50).IsConcurrencyToken();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<OidcAuthorization>(entity =>
        {
            entity.ToTable("IdentityOIDC_Authorizations", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scopes).HasMaxLength(4000);
            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(50);
            entity.Property(x => x.Properties).HasMaxLength(4000);
            entity.Property(x => x.ConcurrencyToken).HasMaxLength(50).IsConcurrencyToken();
            entity.Property(x => x.CreationDate).HasPrecision(3);
            entity.HasOne(x => x.Application).WithMany(x => x.Authorizations)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OidcToken>(entity =>
        {
            entity.ToTable("IdentityOIDC_Tokens", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ReferenceId).HasMaxLength(100);
            entity.Property(x => x.Payload).HasMaxLength(8000);
            entity.Property(x => x.Properties).HasMaxLength(4000);
            entity.Property(x => x.CreationDate).HasPrecision(3);
            entity.Property(x => x.ExpirationDate).HasPrecision(3);
            entity.HasIndex(x => x.ReferenceId).IsUnique()
                  .HasFilter("[ReferenceId] IS NOT NULL");
            entity.HasOne(x => x.Application).WithMany(x => x.Tokens)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Authorization).WithMany(x => x.Tokens)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureScheduling(ModelBuilder builder)
    {
        builder.Entity<ScheduledTaskEntity>(entity =>
        {
            entity.ToTable("Scheduler_Tasks", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.TaskType).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.CronExpression).HasMaxLength(120).IsUnicode(false).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MisfirePolicy).HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(x => x.ConfigurationJson).IsRequired();
            entity.Property(x => x.CreatedByActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedByActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedByActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedByActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.UpdatedAtUtc).HasPrecision(3);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.IsEnabled, x.IsDeleted });
        });

        builder.Entity<ScheduledTaskRunEntity>(entity =>
        {
            entity.ToTable("Scheduler_TaskRuns", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TriggerType).HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsUnicode(false).IsRequired();
            entity.Property(x => x.TriggeredByActorType).HasMaxLength(20);
            entity.Property(x => x.TriggeredByActorId).HasMaxLength(100);
            entity.Property(x => x.TriggeredByActorName).HasMaxLength(100);
            entity.Property(x => x.AuthenticationType).HasMaxLength(50);
            entity.Property(x => x.HostInstanceId).HasMaxLength(200);
            entity.Property(x => x.QuartzFireInstanceId).HasMaxLength(200);
            entity.Property(x => x.LogRelativePath).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ScheduledAtUtc).HasPrecision(3);
            entity.Property(x => x.QueuedAtUtc).HasPrecision(3);
            entity.Property(x => x.StartedAtUtc).HasPrecision(3);
            entity.Property(x => x.LastHeartbeatAtUtc).HasPrecision(3);
            entity.Property(x => x.FinishedAtUtc).HasPrecision(3);
            entity.Property(x => x.CancellationRequestedAtUtc).HasPrecision(3);
            entity.Property(x => x.LogDeletedAtUtc).HasPrecision(3);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TaskId, x.StartedAtUtc });
            entity.HasIndex(x => new { x.Status, x.StartedAtUtc });
            entity.HasIndex(x => x.RootRunId);
        });

        builder.Entity<ScheduledTaskAuditEntity>(entity =>
        {
            entity.ToTable("Scheduler_TaskAudits", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AuthenticationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.TaskId, x.OccurredAtUtc });
            entity.HasIndex(x => x.RunId);
        });
    }

    private static void ConfigureNotification(ModelBuilder builder)
    {
        builder.Entity<NotificationTypeEntity>(entity =>
        {
            entity.ToTable("Notify_Types", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TypeCode).HasMaxLength(50).IsUnicode(false).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.AllowedChannelsJson).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedByActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UpdatedByActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.UpdatedAtUtc).HasPrecision(3);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.TypeCode).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.IsEnabled, x.IsDeleted });
        });

        builder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("Notify_Notifications", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ContentJson).IsRequired();
            entity.Property(x => x.LinkUrl).HasMaxLength(500);
            entity.Property(x => x.Scope).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.TargetIdsJson).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(x => x.SourceModule).HasMaxLength(100);
            entity.Property(x => x.SourceEventKey).HasMaxLength(200);
            entity.Property(x => x.CreatedByActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedByActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.ExpiresAtUtc).HasPrecision(3);
            entity.HasOne(x => x.NotificationType).WithMany().HasForeignKey(x => x.NotificationTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.SourceModule, x.SourceEventKey });
        });

        builder.Entity<NotificationRecipientEntity>(entity =>
        {
            entity.ToTable("Notify_Recipients", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeliveredChannelsJson).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.ReadAtUtc).HasPrecision(3);
            entity.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
        });

        builder.Entity<UserNotificationPreferenceEntity>(entity =>
        {
            entity.ToTable("Notify_UserPreferences", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EnabledChannelsJson).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasPrecision(3);
            entity.HasOne(x => x.NotificationType).WithMany().HasForeignKey(x => x.NotificationTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.NotificationTypeId }).IsUnique();
        });
    }
}

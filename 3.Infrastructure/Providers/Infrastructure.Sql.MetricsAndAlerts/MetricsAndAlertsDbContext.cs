using MetricsAndAlertsApp;
using Microsoft.EntityFrameworkCore;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.MetricsAndAlerts;

[EFDbService("MetricsAndAlerts")]
public sealed class MetricsAndAlertsDbContext : DbContext
{
    public MetricsAndAlertsDbContext(DbContextOptions<MetricsAndAlertsDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MonitorTargetGroup>(entity =>
        {
            entity.ToTable("MonitorTargetGroup", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.GroupName).IsUnique().HasDatabaseName("UX_MonitorTargetGroup_GroupName");
            entity.HasIndex(x => new { x.IsEnabled, x.SortOrder }).HasDatabaseName("IX_MonitorTargetGroup_Enabled_Sort");
            entity.Property(x => x.GroupName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<MonitorTarget>(entity =>
        {
            entity.ToTable("MonitorTarget", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKey, x.TargetType }).IsUnique().HasDatabaseName("UX_MonitorTarget_TargetKey_Type");
            entity.HasIndex(x => x.GroupId).HasDatabaseName("IX_MonitorTarget_GroupId");
            entity.HasIndex(x => new { x.GroupId, x.SortOrder }).HasDatabaseName("IX_MonitorTarget_Group_Sort");
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TargetName).HasMaxLength(256);
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.SortOrder).HasDefaultValue(0);
            entity.Property(x => x.NotifyUsers).HasMaxLength(1024);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<MonitorTargetGroup>()
                .WithMany()
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TargetAliveAlertRule>(entity =>
        {
            entity.ToTable("TargetAliveAlertRule", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKey, x.TargetType }).HasDatabaseName("IX_TargetAliveAlertRule_Target");
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.RuleName).HasMaxLength(256);
            entity.Property(x => x.MinAliveCount).HasDefaultValue(1);
            entity.Property(x => x.OfflineSeconds).HasDefaultValue(60);
            entity.Property(x => x.NotifyMinuteQueue).HasMaxLength(256).HasDefaultValue("0,5,10,120");
            entity.Property(x => x.NotifyUsers).HasMaxLength(1024);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<TargetUrlMonitorRule>(entity =>
        {
            entity.ToTable("TargetUrlMonitorRule", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKey, x.TargetType }).HasDatabaseName("IX_TargetUrlMonitorRule_Target");
            entity.HasIndex(x => new { x.IsEnabled, x.LastCheckAt }).HasDatabaseName("IX_TargetUrlMonitorRule_Enabled_LastCheck");
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.RuleName).HasMaxLength(256);
            entity.Property(x => x.UrlName).HasMaxLength(256);
            entity.Property(x => x.Url).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.HttpMethod).HasMaxLength(16).HasDefaultValue("GET");
            entity.Property(x => x.TimeoutSeconds).HasDefaultValue(10);
            entity.Property(x => x.CheckIntervalSeconds).HasDefaultValue(30);
            entity.Property(x => x.ExpectedStatusCodes).HasMaxLength(128).HasDefaultValue("200");
            entity.Property(x => x.NotifyMinuteQueue).HasMaxLength(256).HasDefaultValue("0,5,10,120");
            entity.Property(x => x.NotifyUsers).HasMaxLength(1024);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<AlertState>(entity =>
        {
            entity.ToTable("AlertState", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AlertKey).IsUnique().HasDatabaseName("UX_AlertState_AlertKey");
            entity.HasIndex(x => new { x.TargetKey, x.TargetType, x.AlertType, x.IsActive }).HasDatabaseName("IX_AlertState_Filter");
            entity.Property(x => x.AlertType).HasConversion<int>();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.AlertKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.CurrentValue).HasMaxLength(256);
            entity.Property(x => x.ThresholdValue).HasMaxLength(256);
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<AlertNotifyLog>(entity =>
        {
            entity.ToTable("AlertNotifyLog", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKey, x.TargetType, x.AlertType, x.CreatedAt }).HasDatabaseName("IX_AlertNotifyLog_Filter");
            entity.Property(x => x.AlertType).HasConversion<int>();
            entity.Property(x => x.NotifyType).HasConversion<int>();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<MetricAlertRule>(entity =>
        {
            entity.ToTable("MetricAlertRule", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKey, x.TargetType, x.IsEnabled }).HasDatabaseName("IX_MetricAlertRule_Target_Enabled");
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.RuleName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.QueryItemsJson).IsRequired();
            entity.Property(x => x.Formula).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.NotifyMinuteQueue).HasMaxLength(256).HasDefaultValue("0,5,10,120");
            entity.Property(x => x.NotifyUsers).HasMaxLength(1024);
            entity.Property(x => x.AggregateInstances).HasDefaultValue(false);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<ScheduledMetricMessageRule>(entity =>
        {
            entity.ToTable("ScheduledMetricMessageRule", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TargetKey, x.TargetType, x.IsEnabled }).HasDatabaseName("IX_ScheduledMetricMessageRule_Target_Enabled");
            entity.HasIndex(x => new { x.IsEnabled, x.NextRunAt }).HasDatabaseName("IX_ScheduledMetricMessageRule_Due");
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.RuleName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.QueryItemsJson).IsRequired();
            entity.Property(x => x.MessageTemplate).IsRequired();
            entity.Property(x => x.Cron).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(128).HasDefaultValue("Asia/Shanghai");
            entity.Property(x => x.NotifyUsers).HasMaxLength(1024);
            entity.Property(x => x.AggregateInstances).HasDefaultValue(false);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<ScheduledMessageSendLog>(entity =>
        {
            entity.ToTable("ScheduledMessageSendLog", "dbo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RuleId, x.CreatedAt }).HasDatabaseName("IX_ScheduledMessageSendLog_Rule_CreatedAt");
            entity.HasIndex(x => new { x.TargetKey, x.TargetType, x.CreatedAt }).HasDatabaseName("IX_ScheduledMessageSendLog_Target_CreatedAt");
            entity.Property(x => x.TargetKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }

    public DbSet<MonitorTargetGroup> MonitorTargetGroups => Set<MonitorTargetGroup>();
    public DbSet<MonitorTarget> MonitorTargets => Set<MonitorTarget>();
    public DbSet<TargetAliveAlertRule> TargetAliveAlertRules => Set<TargetAliveAlertRule>();
    public DbSet<TargetUrlMonitorRule> TargetUrlMonitorRules => Set<TargetUrlMonitorRule>();
    public DbSet<AlertState> AlertStates => Set<AlertState>();
    public DbSet<AlertNotifyLog> AlertNotifyLogs => Set<AlertNotifyLog>();
    public DbSet<MetricAlertRule> MetricAlertRules => Set<MetricAlertRule>();
    public DbSet<ScheduledMetricMessageRule> ScheduledMetricMessageRules => Set<ScheduledMetricMessageRule>();
    public DbSet<ScheduledMessageSendLog> ScheduledMessageSendLogs => Set<ScheduledMessageSendLog>();
}

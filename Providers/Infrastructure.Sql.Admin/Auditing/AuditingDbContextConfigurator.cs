using Infrastructure.Sql.Admin.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Sql.Admin;

public static class AuditingDbContextConfigurator
{
    public static void ConfigureAuditing(this ModelBuilder builder)
    {
        builder.Entity<SecurityEventLog>(entity =>
        {
            entity.ToTable("Auditing_SecurityEvents", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.Property(x => x.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AuthenticationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
            entity.HasIndex(x => x.OccurredAtUtc);
        });

        builder.Entity<RequestAuditLog>(entity =>
        {
            entity.ToTable("Auditing_RequestLogs", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AuthenticationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.QueryString).HasMaxLength(1000);
            entity.Property(x => x.ControllerName).HasMaxLength(100);
            entity.Property(x => x.ActionName).HasMaxLength(100);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(3);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.ActorId, x.OccurredAtUtc });
        });
    }
}

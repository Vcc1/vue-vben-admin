using ApiGateway.Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Sql.Admin;

public static class GatewayDbContextConfigurator
{
    public static void ConfigureGateway(this ModelBuilder builder)
    {
        builder.Entity<GatewayApi>(entity =>
        {
            entity.ToTable("Gateway_Apis", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ApiKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HeadersJson).HasMaxLength(4000);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.Property(x => x.UpdatedAtUtc).HasPrecision(3);
            entity.HasIndex(x => x.ApiKey).IsUnique();
            entity.HasIndex(x => x.IsEnabled);
        });

        builder.Entity<GatewayModule>(entity =>
        {
            entity.ToTable("Gateway_Modules", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PathPrefix).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HeadersJson).HasMaxLength(4000);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.HasOne<GatewayApi>().WithMany().HasForeignKey(x => x.ApiId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ApiId, x.PathPrefix }).IsUnique();
            entity.HasIndex(x => new { x.ApiId, x.IsEnabled });
        });

        builder.Entity<GatewayEndpoint>(entity =>
        {
            entity.ToTable("Gateway_Endpoints", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
            entity.Property(x => x.OperationId).HasMaxLength(200);
            entity.Property(x => x.Summary).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.HasOne<GatewayModule>().WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ModuleId, x.HttpMethod, x.Path }).IsUnique();
            entity.HasIndex(x => new { x.ModuleId, x.IsEnabled });
        });

        builder.Entity<GatewayPermission>(entity =>
        {
            entity.ToTable("Gateway_Permissions", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RoleName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.RoleName, x.ModuleId, x.EndpointId }).IsUnique();
            entity.HasIndex(x => new { x.ModuleId, x.IsEnabled });
        });

        builder.Entity<GatewayAuditLog>(entity =>
        {
            entity.ToTable("Gateway_AuditLogs", "dbo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApiName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ModuleName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ActorType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AuthenticationType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.OccurredAtUtc).HasPrecision(3);
            entity.HasIndex(x => new { x.ApiId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.ModuleId, x.OccurredAtUtc });
            entity.HasIndex(x => x.OccurredAtUtc);
        });
    }
}

#nullable enable

using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class HotelMatchVeto : IHasEntityConfiguration<HotelMatchVeto>
{
    public long Id { get; set; }
    public int SupHotelId { get; set; }
    public string MTHotelCode { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int RedisSyncAttemptCount { get; set; }
    public DateTime? FirstRedisSyncFailureTime { get; set; }
    public DateTime? LastRedisSyncAttemptTime { get; set; }
    public string? LastRedisSyncError { get; set; }
    public DateTime? FailureAlertSentTime { get; set; }
    public DateTime? RedisSyncedTime { get; set; }
    public DateTime? RecoveryAlertSentTime { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public SupHotelInfoClean SupplierHotel { get; set; } = null!;

    public static void Configure(EntityTypeBuilder<HotelMatchVeto> builder)
    {
        builder.ToTable("HotelMatchVeto", "dbo");
        builder.HasKey(veto => veto.Id).HasName("PK_HotelMatchVeto");
        builder.Property(veto => veto.MTHotelCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(veto => veto.Operator).HasMaxLength(100).IsRequired();
        builder.Property(veto => veto.Reason).HasMaxLength(500).IsRequired();
        builder.Property(veto => veto.LastRedisSyncError).HasMaxLength(4000);
        builder.Property(veto => veto.FirstRedisSyncFailureTime).HasColumnType("datetime2(3)");
        builder.Property(veto => veto.LastRedisSyncAttemptTime).HasColumnType("datetime2(3)");
        builder.Property(veto => veto.FailureAlertSentTime).HasColumnType("datetime2(3)");
        builder.Property(veto => veto.RedisSyncedTime).HasColumnType("datetime2(3)");
        builder.Property(veto => veto.RecoveryAlertSentTime).HasColumnType("datetime2(3)");
        builder.Property(veto => veto.CreateTime).HasColumnType("datetime2(3)");
        builder.Property(veto => veto.UpdateTime).HasColumnType("datetime2(3)");
        builder.HasOne(veto => veto.SupplierHotel)
            .WithMany()
            .HasForeignKey(veto => veto.SupHotelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(veto => new { veto.SupHotelId, veto.MTHotelCode })
            .IsUnique()
            .HasDatabaseName("UX_HotelMatchVeto_Pair");
        builder.HasIndex(veto => new
            {
                veto.RedisSyncedTime,
                veto.RecoveryAlertSentTime,
                veto.Id
            })
            .HasDatabaseName("IX_HotelMatchVeto_PendingSync");
    }
}

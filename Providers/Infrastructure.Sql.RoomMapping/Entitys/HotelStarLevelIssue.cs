using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class HotelStarLevelIssue : IHasEntityConfiguration<HotelStarLevelIssue>
{
    public long Id { get; set; }
    public byte SourceType { get; set; }
    public int SupplierId { get; set; }
    public int SourceId { get; set; }
    public string RawStarLevel { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public DateTime FirstSeenTime { get; set; }

    public static void Configure(EntityTypeBuilder<HotelStarLevelIssue> builder)
    {
        builder.ToTable("HotelStarLevelIssue", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_HotelStarLevelIssue");
        builder.Property(x => x.RawStarLevel).HasMaxLength(100).UseCollation("Chinese_PRC_BIN2").IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.FirstSeenTime).HasColumnType("datetime2(3)");
        builder.HasIndex(x => new { x.SourceType, x.SupplierId, x.RawStarLevel })
            .IsUnique()
            .HasDatabaseName("UX_HotelStarLevelIssue_SourceSupplierRawValue");
    }
}

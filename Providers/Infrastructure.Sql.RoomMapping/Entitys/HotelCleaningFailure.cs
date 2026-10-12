using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class HotelCleaningFailure : IHasEntityConfiguration<HotelCleaningFailure>
{
    public long Id { get; set; }
    public byte SourceType { get; set; }
    public long DataId { get; set; }
    public int? SupplierId { get; set; }
    public int? SourceId { get; set; }
    public string HotelCode { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public int CleanVersion { get; set; }
    public int RegionBoundaryVersion { get; set; }
    public int FailureCount { get; set; }
    public DateTime FirstFailureTime { get; set; }
    public DateTime LastFailureTime { get; set; }

    public static void Configure(EntityTypeBuilder<HotelCleaningFailure> builder)
    {
        builder.ToTable("HotelCleaningFailure", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_HotelCleaningFailure");
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.HotelCode).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExceptionType).HasMaxLength(500).IsUnicode(false).IsRequired();
        builder.Property(x => x.ExceptionMessage).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.StackTrace).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FailureCount).HasDefaultValue(1);
        builder.Property(x => x.FirstFailureTime).HasColumnType("datetime2(3)");
        builder.Property(x => x.LastFailureTime).HasColumnType("datetime2(3)");
        builder.HasIndex(x => new { x.SourceType, x.DataId })
            .IsUnique()
            .HasDatabaseName("UX_HotelCleaningFailure_SourceType_DataId");
        builder.HasIndex(x => new
            {
                x.SourceType,
                x.CleanVersion,
                x.RegionBoundaryVersion,
                x.DataId
            })
            .HasDatabaseName("IX_HotelCleaningFailure_CleaningSnapshot");
    }
}

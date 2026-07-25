using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class HotelMatchConflict : IHasEntityConfiguration<HotelMatchConflict>
{
    public long Id { get; set; }
    public int SupHotelId { get; set; }
    public int SupId { get; set; }
    public string SupHotelCode { get; set; } = string.Empty;
    public int MatchingVersion { get; set; }
    public int CandidateCount { get; set; }
    public string MTHotelCodesJson { get; set; } = "[]";
    public string CandidatesJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public DateTime FirstConflictTime { get; set; }
    public DateTime LastConflictTime { get; set; }
    public DateTime? ResolvedTime { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public SupHotelInfoClean SupplierHotel { get; set; } = null!;

    public static void Configure(EntityTypeBuilder<HotelMatchConflict> builder)
    {
        builder.ToTable("HotelMatchConflict", "dbo");
        builder.HasKey(conflict => conflict.Id).HasName("PK_HotelMatchConflict");
        builder.Property(conflict => conflict.SupHotelCode).HasMaxLength(50).IsRequired();
        builder.Property(conflict => conflict.MTHotelCodesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(conflict => conflict.CandidatesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(conflict => conflict.FirstConflictTime).HasColumnType("datetime2(3)");
        builder.Property(conflict => conflict.LastConflictTime).HasColumnType("datetime2(3)");
        builder.Property(conflict => conflict.ResolvedTime).HasColumnType("datetime2(3)");
        builder.Property(conflict => conflict.CreateTime).HasColumnType("datetime2(3)");
        builder.Property(conflict => conflict.UpdateTime).HasColumnType("datetime2(3)");
        builder.HasOne(conflict => conflict.SupplierHotel)
            .WithMany()
            .HasForeignKey(conflict => conflict.SupHotelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(conflict => conflict.SupHotelId)
            .IsUnique()
            .HasDatabaseName("UX_HotelMatchConflict_SupHotelId");
        builder.HasIndex(conflict => new { conflict.IsActive, conflict.UpdateTime })
            .HasDatabaseName("IX_HotelMatchConflict_Active");
    }
}

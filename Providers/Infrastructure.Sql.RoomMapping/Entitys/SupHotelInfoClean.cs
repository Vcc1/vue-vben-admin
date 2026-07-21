using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class SupHotelInfoClean : IHasEntityConfiguration<SupHotelInfoClean>
{
    public int SupHotelId { get; set; }
    public int SupId { get; set; }
    public string SHotelCode { get; set; } = string.Empty;
    public string MTHotelCode { get; set; } = string.Empty;
    public int State { get; set; }
    public int? SSaleTopLevel { get; set; }
    public int? IsHandMatch { get; set; }
    public int? ReMatchState { get; set; }
    public string? HotelName { get; set; }
    public string? HotelNameEn { get; set; }
    public string? Tel { get; set; }
    public ICollection<SupHotelPhone> Phones { get; set; } = new List<SupHotelPhone>();
    public string? Fax { get; set; }
    public string? Address { get; set; }
    public string? PostCode { get; set; }
    public string? Email { get; set; }
    public decimal? StarLevel { get; set; }
    public byte CoordType { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? Latitude { get; set; }
    public string? RegionLevel1Code { get; set; }
    public string? RegionLevel1Name { get; set; }
    public string? RegionLevel2Code { get; set; }
    public string? RegionLevel2Name { get; set; }
    public string? RegionLevel3Code { get; set; }
    public string? RegionLevel3Name { get; set; }
    public byte RegionResolveStatus { get; set; }
    public int RegionFenceVersion { get; set; }
    public long SourceDataVersion { get; set; }
    public byte[]? SourceDataHash { get; set; }
    public byte[]? NormalizedDataHash { get; set; }
    public int CleanVersion { get; set; }
    public byte CleanStatus { get; set; }
    public string? CleanError { get; set; }
    public DateTime CleanTime { get; set; }
    public byte DataQualityStatus { get; set; }
    public long DataQualityFlags { get; set; }
    public string? DataQualityDetailsJson { get; set; }
    public byte MatchStatus { get; set; }
    public int? LastMatchVersion { get; set; }
    public byte[]? LastMatchedDataHash { get; set; }
    public DateTime? LastMatchTime { get; set; }
    public DateTime? NextMatchTime { get; set; }
    public int MatchAttemptCount { get; set; }
    public string? MatchError { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    public static void Configure(EntityTypeBuilder<SupHotelInfoClean> builder)
    {
        const string collation = "Chinese_PRC_CI_AS";

        builder.ToTable("SupHotelInfo", "dbo");
        builder.HasKey(x => x.SupHotelId).HasName("PK_SupHotelInfo");
        builder.Property(x => x.SupHotelId).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.SHotelCode).HasMaxLength(50).UseCollation(collation).IsRequired();
        builder.Property(x => x.MTHotelCode).HasMaxLength(100).IsUnicode(false).UseCollation(collation).IsRequired();
        builder.Property(x => x.HotelName).HasMaxLength(200).UseCollation(collation);
        builder.Property(x => x.HotelNameEn).HasMaxLength(200).UseCollation(collation);
        builder.Property(x => x.Tel).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.Fax).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.Address).HasMaxLength(500).UseCollation(collation);
        builder.Property(x => x.PostCode).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Email).HasMaxLength(100).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.SourceDataHash).HasMaxLength(32);
        builder.Property(x => x.NormalizedDataHash).HasMaxLength(32);
        builder.Property(x => x.LastMatchedDataHash).HasMaxLength(32);
        builder.Property(x => x.CleanTime).HasColumnType("datetime2(3)");
        builder.Property(x => x.CreateTime).HasColumnType("datetime2(3)");
        builder.Property(x => x.UpdateTime).HasColumnType("datetime2(3)");
        builder.Property(x => x.CleanError).HasMaxLength(1000).UseCollation(collation);
        builder.Property(x => x.MatchError).HasMaxLength(1000).UseCollation(collation);
        builder.HasOne<SupHotelInfoRaw>().WithOne()
            .HasForeignKey<SupHotelInfoClean>(x => x.SupHotelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SupId, x.SHotelCode }).IsUnique()
            .HasDatabaseName("UX_SupHotelInfo_SupplierHotel");
        builder.HasIndex(x => new { x.MatchStatus, x.LastMatchVersion, x.CleanStatus, x.DataQualityStatus })
            .HasDatabaseName("IX_SupHotelInfo_Match");
        builder.HasIndex(x => new { x.SupId, x.SHotelCode, x.MTHotelCode })
            .HasDatabaseName("IX_SupHotelInfo_Mapping");
        builder.HasIndex(x => x.MTHotelCode).HasDatabaseName("IX_SupHotelInfo_MTHotelCode");
    }
}

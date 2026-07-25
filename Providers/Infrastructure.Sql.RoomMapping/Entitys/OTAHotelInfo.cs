using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class OTAHotelInfo : IHasEntityConfiguration<OTAHotelInfo>
{
    public int HotelID { get; set; }
    public string MTHotelCode { get; set; } = string.Empty;
    public string HotelName { get; set; } = string.Empty;
    public string? HotelEnName { get; set; }
    public string? EnAddress { get; set; }
    public int Estate { get; set; }
    public byte Flag { get; set; }
    public int AreaID { get; set; }
    public int CountryID { get; set; }
    public int ProvinceID { get; set; }
    public int RootCityID { get; set; }
    public int CityID { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? CountryNameEn { get; set; }
    public string? CityCode { get; set; }
    public string? CityName { get; set; }
    public string? CityNameEn { get; set; }
    public string? Address { get; set; }
    public string? Tel { get; set; }
    public string? Fax { get; set; }
    public string? PostCode { get; set; }
    public string? Email { get; set; }
    public string? WebSite { get; set; }
    public decimal? StarLevel { get; set; }
    public byte CoordType { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? Latitude { get; set; }
    public long SourceDataVersion { get; set; }
    public byte[]? SourceDataHash { get; set; }
    public byte[]? NormalizedDataHash { get; set; }
    public ICollection<OTAHotelPhone> Phones { get; set; } = new List<OTAHotelPhone>();
    public string? RegionLevel1Code { get; set; }
    public string? RegionLevel1Name { get; set; }
    public string? RegionLevel2Code { get; set; }
    public string? RegionLevel2Name { get; set; }
    public string? RegionLevel3Code { get; set; }
    public string? RegionLevel3Name { get; set; }
    public byte RegionResolveStatus { get; set; }
    public int RegionFenceVersion { get; set; }
    public int CleanVersion { get; set; }
    public byte CleanStatus { get; set; }
    public string? CleanError { get; set; }
    public byte DataQualityStatus { get; set; }
    public long DataQualityFlags { get; set; }
    public string? DataQualityDetailsJson { get; set; }
    public DateTime? SourceLastModifyTime { get; set; }
    public DateTime CleanTime { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    public static void Configure(EntityTypeBuilder<OTAHotelInfo> builder)
    {
        const string collation = "Chinese_PRC_CI_AS";

        builder.ToTable("OTAHotelInfo", "dbo", table => table.UseSqlOutputClause(false));
        builder.HasKey(x => x.HotelID).HasName("PK_OTAHotelInfo");
        builder.Property(x => x.HotelID).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.MTHotelCode).HasMaxLength(100).IsUnicode(false).UseCollation(collation).IsRequired();
        builder.Property(x => x.HotelName).HasMaxLength(200).UseCollation(collation).IsRequired();
        builder.Property(x => x.HotelEnName).HasMaxLength(300).UseCollation(collation);
        builder.Property(x => x.CityCode).HasMaxLength(90).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Address).HasMaxLength(300).UseCollation(collation);
        builder.Property(x => x.EnAddress).HasMaxLength(500).UseCollation(collation);
        builder.Property(x => x.CountryName).HasMaxLength(64).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.CountryCode).HasMaxLength(128).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.CountryNameEn).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.CityName).HasMaxLength(64).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.CityNameEn).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.Tel).HasMaxLength(60).UseCollation(collation);
        builder.Property(x => x.Fax).HasMaxLength(60).UseCollation(collation);
        builder.Property(x => x.PostCode).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Email).HasMaxLength(100).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.WebSite).HasMaxLength(500).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.StarLevel).HasPrecision(3, 1);
        builder.Property(x => x.SourceDataHash).HasMaxLength(32);
        builder.Property(x => x.NormalizedDataHash).HasMaxLength(32);
        builder.Property(x => x.SourceLastModifyTime).HasColumnType("datetime");
        builder.Property(x => x.CleanTime).HasColumnType("datetime");
        builder.Property(x => x.CreateTime).HasColumnType("datetime");
        builder.Property(x => x.UpdateTime).HasColumnType("datetime");
        builder.Property(x => x.CleanError).HasMaxLength(1000).UseCollation(collation);
        builder.HasOne<OTAHotelInfoRaw>().WithOne()
            .HasForeignKey<OTAHotelInfo>(x => x.HotelID).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.MTHotelCode).HasDatabaseName("IX_OTAHotelInfo_MTHotelCode");
        builder.HasIndex(x => new { x.Estate, x.CleanStatus, x.DataQualityStatus, x.CleanVersion })
            .HasDatabaseName("IX_OTAHotelInfo_Clean");
        builder.HasIndex(x => new { x.CleanVersion, x.RegionFenceVersion, x.HotelID })
            .IncludeProperties(x => new { x.SourceDataVersion, x.CleanTime })
            .HasDatabaseName("IX_OTAHotelInfo_CleaningSnapshot");
    }
}

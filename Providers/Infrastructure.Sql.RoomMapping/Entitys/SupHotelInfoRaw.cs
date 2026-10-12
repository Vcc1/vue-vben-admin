using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class SupHotelInfoRaw : IHasEntityConfiguration<SupHotelInfoRaw>
{
    public int SupHotelId { get; set; }
    public int SupId { get; set; }
    public string SHotelCode { get; set; } = string.Empty;
    public int State { get; set; }
    public string MTHotelCode { get; set; } = string.Empty;
    public int? SSaleTopLevel { get; set; }
    public string? LastUpdatedBy { get; set; }
    public DateTime? SourceLastUpdateDate { get; set; }
    public int? IsHandMatch { get; set; }
    public decimal? Similarity { get; set; }
    public int? ReMatchState { get; set; }
    public string? ReStateBy { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? CityCode { get; set; }
    public string? CityName { get; set; }
    public string? HotelName { get; set; }
    public string? HotelNameEn { get; set; }
    public string? StarLevel { get; set; }
    public string? Tel { get; set; }
    public string? Fax { get; set; }
    public string? GpsType { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? Latitude { get; set; }
    public string? Address { get; set; }
    public string? PostCode { get; set; }
    public string? Email { get; set; }
    public string? Data { get; set; }
    public string? SourceCreateBy { get; set; }
    public DateTime? SourceCreateDate { get; set; }
    public string? SourceUpdateBy { get; set; }
    public DateTime? SourceUpdateDate { get; set; }
    public DateTime LastSyncTime { get; set; }
    public DateTime DataChangeTime { get; set; }
    public long DataVersion { get; set; }
    public byte[]? DataHash { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    public static void Configure(EntityTypeBuilder<SupHotelInfoRaw> builder)
    {
        const string collation = "Chinese_PRC_CI_AS";

        builder.ToTable("SupHotelInfoRaw", "dbo");
        builder.HasKey(x => x.SupHotelId).HasName("PK_SupHotelInfoRaw");
        builder.Property(x => x.SupHotelId).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.SHotelCode).HasMaxLength(50).UseCollation(collation).IsRequired();
        builder.Property(x => x.MTHotelCode).HasMaxLength(12).IsUnicode(false).UseCollation(collation).IsRequired();
        builder.Property(x => x.LastUpdatedBy).HasMaxLength(50).UseCollation(collation);
        builder.Property(x => x.SourceLastUpdateDate).HasColumnType("datetime");
        builder.Property(x => x.Similarity).HasPrecision(6, 2);
        builder.Property(x => x.ReStateBy).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.CountryCode).HasMaxLength(50).UseCollation(collation);
        builder.Property(x => x.CountryName).HasMaxLength(50).UseCollation(collation);
        builder.Property(x => x.CityCode).HasMaxLength(50).UseCollation(collation);
        builder.Property(x => x.CityName).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.HotelName).HasMaxLength(200).UseCollation(collation);
        builder.Property(x => x.HotelNameEn).HasMaxLength(200).UseCollation(collation);
        builder.Property(x => x.StarLevel).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Tel).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.Fax).HasMaxLength(100).UseCollation(collation);
        builder.Property(x => x.GpsType).HasMaxLength(1).IsFixedLength().IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Longitude).HasPrecision(15, 10);
        builder.Property(x => x.Latitude).HasPrecision(15, 10);
        builder.Property(x => x.Address).HasMaxLength(500).UseCollation(collation);
        builder.Property(x => x.PostCode).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Email).HasMaxLength(100).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Data).HasMaxLength(60).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.SourceCreateBy).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.SourceCreateDate).HasColumnType("datetime");
        builder.Property(x => x.SourceUpdateBy).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.SourceUpdateDate).HasColumnType("datetime");
        builder.Property(x => x.LastSyncTime).HasColumnType("datetime");
        builder.Property(x => x.DataChangeTime).HasColumnType("datetime");
        builder.Property(x => x.CreateTime).HasColumnType("datetime");
        builder.Property(x => x.UpdateTime).HasColumnType("datetime");
        builder.Property(x => x.DataHash).HasMaxLength(32);
        builder.HasIndex(x => new { x.DataChangeTime, x.SupHotelId }).HasDatabaseName("IX_SupHotelInfoRaw_DataChangeTime");
        builder.HasIndex(x => new { x.DataVersion, x.SupHotelId }).HasDatabaseName("IX_SupHotelInfoRaw_DataVersion");
    }
}

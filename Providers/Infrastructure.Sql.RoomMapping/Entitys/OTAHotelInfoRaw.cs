#nullable enable

using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class OTAHotelInfoRaw : IHasEntityConfiguration<OTAHotelInfoRaw>
{
    public long Id { get; set; }

    public int HotelID { get; set; }

    public string MTHotelCode { get; set; } = null!;

    public string HotelName { get; set; } = null!;

    public string? HotelEnName { get; set; }

    public string? CityCode { get; set; }

    public string? Address { get; set; }

    public string? EnAddress { get; set; }

    public int Estate { get; set; }

    public byte Flag { get; set; }

    public int AreaID { get; set; }

    public int CountryID { get; set; }

    public int ProvinceID { get; set; }

    public int RootCityID { get; set; }

    public int CityID { get; set; }

    public string? CountryName { get; set; }

    public string? CountryCode { get; set; }

    public string? CountryNameEn { get; set; }

    public string? CityName { get; set; }

    public string? CityNameEn { get; set; }

    public string? Tel { get; set; }

    public string? Fax { get; set; }

    public string? PostCode { get; set; }

    public string? Email { get; set; }

    public string? WebSite { get; set; }

    public string? StarLevel { get; set; }

    public int? GpsType { get; set; }

    public string? Longitude { get; set; }

    public string? Latitude { get; set; }

    public DateTime? LastModifyTime { get; set; }

    public DateTime LastSyncTime { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    public static void Configure(EntityTypeBuilder<OTAHotelInfoRaw> builder)
    {
        const string collation = "Chinese_PRC_CI_AS";

        builder.ToTable("OTAHotelInfoRaw", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_OTAHotelInfoRaw");

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.HotelID).IsRequired();
        builder.Property(x => x.MTHotelCode).IsRequired().HasMaxLength(100).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.HotelName).IsRequired().HasMaxLength(200).UseCollation(collation);
        builder.Property(x => x.HotelEnName).HasMaxLength(300).UseCollation(collation);
        builder.Property(x => x.CityCode).HasMaxLength(90).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Address).HasMaxLength(300).UseCollation(collation);
        builder.Property(x => x.EnAddress).HasMaxLength(500).UseCollation(collation);
        builder.Property(x => x.Estate).IsRequired();
        builder.Property(x => x.Flag).IsRequired().HasDefaultValue((byte)0);
        builder.Property(x => x.AreaID).IsRequired();
        builder.Property(x => x.CountryID).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ProvinceID).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.RootCityID).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CityID).IsRequired().HasDefaultValue(0);
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
        builder.Property(x => x.StarLevel).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.GpsType);
        builder.Property(x => x.Longitude).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.Latitude).HasMaxLength(50).IsUnicode(false).UseCollation(collation);
        builder.Property(x => x.LastModifyTime).HasColumnType("datetime");
        builder.Property(x => x.LastSyncTime).IsRequired().HasColumnType("datetime").HasDefaultValueSql("getdate()").ValueGeneratedOnAdd();
        builder.Property(x => x.CreateTime).IsRequired().HasColumnType("datetime").HasDefaultValueSql("getdate()").ValueGeneratedOnAdd();
        builder.Property(x => x.UpdateTime).IsRequired().HasColumnType("datetime").HasDefaultValueSql("getdate()").ValueGeneratedOnAdd();
    }
}

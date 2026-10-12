#nullable enable

using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class OTAHotelPhone : IHasEntityConfiguration<OTAHotelPhone>
{
    public int HotelID { get; set; }
    public short Ordinal { get; set; }
    public string SourcePhone { get; set; } = string.Empty;
    public string? NormalizedPhone { get; set; }
    public string? CountryIsoCode { get; set; }
    public string? NationalNumber { get; set; }
    public string? CountryCallingCode { get; set; }
    public string? NumberType { get; set; }
    public string? AreaCode { get; set; }
    public string? Extension { get; set; }
    public bool IsValid { get; set; }
    public string? ParseError { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public OTAHotelInfo Hotel { get; set; } = null!;

    public static void Configure(EntityTypeBuilder<OTAHotelPhone> builder)
    {
        builder.ToTable("OTAHotelPhone", "dbo");
        builder.HasKey(x => new { x.HotelID, x.Ordinal }).HasName("PK_OTAHotelPhone");
        ConfigureColumns(builder);
        builder.HasOne(x => x.Hotel).WithMany(x => x.Phones).HasForeignKey(x => x.HotelID).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.NormalizedPhone).HasFilter("[IsValid] = 1 AND [NormalizedPhone] IS NOT NULL");
        builder.HasIndex(x => new { x.CountryCallingCode, x.NationalNumber })
            .HasFilter("[IsValid] = 1 AND [CountryCallingCode] IS NOT NULL AND [NationalNumber] IS NOT NULL");
    }

    static void ConfigureColumns(EntityTypeBuilder<OTAHotelPhone> builder)
    {
        builder.Property(x => x.HotelID).ValueGeneratedNever().IsRequired();
        builder.Property(x => x.SourcePhone).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedPhone).HasMaxLength(32).IsUnicode(false);
        builder.Property(x => x.CountryIsoCode).HasMaxLength(2).IsUnicode(false);
        builder.Property(x => x.NationalNumber).HasMaxLength(32).IsUnicode(false);
        builder.Property(x => x.CountryCallingCode).HasMaxLength(8).IsUnicode(false);
        builder.Property(x => x.NumberType).HasMaxLength(32).IsUnicode(false);
        builder.Property(x => x.AreaCode).HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.Extension).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.ParseError).HasMaxLength(200).IsUnicode(false);
        builder.Property(x => x.CreateTime).HasColumnType("datetime2(3)");
        builder.Property(x => x.UpdateTime).HasColumnType("datetime2(3)");
    }
}
#nullable enable

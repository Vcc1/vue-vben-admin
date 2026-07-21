#nullable enable

using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class SupHotelPhone : IHasEntityConfiguration<SupHotelPhone>
{
    public long Id { get; set; }
    public int SupHotelId { get; set; }
    public short Ordinal { get; set; }
    public string Raw { get; set; } = string.Empty;
    public string? E164 { get; set; }
    public string? NationalNumber { get; set; }
    public string? CountryCallingCode { get; set; }
    public string? RegionCode { get; set; }
    public string? NumberType { get; set; }
    public string? AreaCode { get; set; }
    public string? Extension { get; set; }
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public SupHotelInfoClean Hotel { get; set; } = null!;

    public static void Configure(EntityTypeBuilder<SupHotelPhone> builder)
    {
        builder.ToTable("SupHotelPhone", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_SupHotelPhone");
        builder.Property(x => x.Raw).HasMaxLength(100).IsRequired();
        builder.Property(x => x.E164).HasMaxLength(32).IsUnicode(false);
        builder.Property(x => x.NationalNumber).HasMaxLength(32).IsUnicode(false);
        builder.Property(x => x.CountryCallingCode).HasMaxLength(8).IsUnicode(false);
        builder.Property(x => x.RegionCode).HasMaxLength(2).IsUnicode(false);
        builder.Property(x => x.NumberType).HasMaxLength(32).IsUnicode(false);
        builder.Property(x => x.AreaCode).HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.Extension).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Error).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.CreateTime).HasColumnType("datetime2(3)");
        builder.Property(x => x.UpdateTime).HasColumnType("datetime2(3)");
        builder.HasOne(x => x.Hotel).WithMany(x => x.Phones).HasForeignKey(x => x.SupHotelId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.SupHotelId, x.Ordinal }).IsUnique();
        builder.HasIndex(x => x.E164).HasFilter("[IsValid] = 1 AND [E164] IS NOT NULL");
        builder.HasIndex(x => new { x.CountryCallingCode, x.NationalNumber })
            .HasFilter("[IsValid] = 1 AND [CountryCallingCode] IS NOT NULL AND [NationalNumber] IS NOT NULL");
    }
}
#nullable enable

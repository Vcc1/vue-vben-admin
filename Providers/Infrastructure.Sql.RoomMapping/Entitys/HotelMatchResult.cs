#nullable enable

using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class HotelMatchResult : IHasEntityConfiguration<HotelMatchResult>
{
    public long Id { get; set; }
    public int SupHotelId { get; set; }
    public string MTHotelCode { get; set; } = string.Empty;
    public byte NameEvidenceSourceType { get; set; }
    public int NameEvidenceHotelId { get; set; }
    public byte NameMatchKind { get; set; }
    public string? NamePendingValue { get; set; }
    public string? NameEvidenceValue { get; set; }
    public byte CoordinateEvidenceSourceType { get; set; }
    public int CoordinateEvidenceHotelId { get; set; }
    public decimal DistanceMeters { get; set; }
    public byte? StarEvidenceSourceType { get; set; }
    public int? StarEvidenceHotelId { get; set; }
    public string? StarPendingValue { get; set; }
    public string? StarEvidenceValue { get; set; }
    public byte? AddressEvidenceSourceType { get; set; }
    public int? AddressEvidenceHotelId { get; set; }
    public string? AddressPendingValue { get; set; }
    public string? AddressEvidenceValue { get; set; }
    public byte? PhoneEvidenceSourceType { get; set; }
    public int? PhoneEvidenceHotelId { get; set; }
    public string? PhonePendingValue { get; set; }
    public string? PhoneEvidenceValue { get; set; }
    public int OtaFactCount { get; set; }
    public int SupplierFactCount { get; set; }
    public string? EvidenceJson { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public SupHotelInfoClean SupplierHotel { get; set; } = null!;

    public static void Configure(EntityTypeBuilder<HotelMatchResult> builder)
    {
        builder.ToTable("HotelMatchResult", "dbo");
        builder.HasKey(result => result.Id).HasName("PK_HotelMatchResult");
        builder.Property(result => result.MTHotelCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(result => result.NamePendingValue).HasMaxLength(300);
        builder.Property(result => result.NameEvidenceValue).HasMaxLength(300);
        builder.Property(result => result.DistanceMeters).HasPrecision(12, 3);
        builder.Property(result => result.StarPendingValue).HasMaxLength(20).IsUnicode(false);
        builder.Property(result => result.StarEvidenceValue).HasMaxLength(20).IsUnicode(false);
        builder.Property(result => result.AddressPendingValue).HasMaxLength(500);
        builder.Property(result => result.AddressEvidenceValue).HasMaxLength(500);
        builder.Property(result => result.PhonePendingValue).HasMaxLength(32).IsUnicode(false);
        builder.Property(result => result.PhoneEvidenceValue).HasMaxLength(32).IsUnicode(false);
        builder.Property(result => result.EvidenceJson).HasColumnType("nvarchar(max)");
        builder.Property(result => result.CreateTime).HasColumnType("datetime2(3)");
        builder.Property(result => result.UpdateTime).HasColumnType("datetime2(3)");
        builder.HasOne(result => result.SupplierHotel)
            .WithMany()
            .HasForeignKey(result => result.SupHotelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(result => result.SupHotelId)
            .IsUnique()
            .HasDatabaseName("UX_HotelMatchResult_BusinessKey");
        builder.HasIndex(result => result.MTHotelCode)
            .HasDatabaseName("IX_HotelMatchResult_MTHotelCode");
    }
}

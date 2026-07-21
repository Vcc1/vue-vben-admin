using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class HotelMatchDelivery : IHasEntityConfiguration<HotelMatchDelivery>
{
    public long Id { get; set; }
    public int SupId { get; set; }
    public string SHotelCode { get; set; } = string.Empty;
    public string MTHotelCode { get; set; } = string.Empty;
    public long SourceDataVersion { get; set; }
    public byte[] NormalizedDataHash { get; set; } = [];
    public int CleanVersion { get; set; }
    public int MatchingVersion { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public byte DeliveryStatus { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryTime { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? LastError { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    public static void Configure(EntityTypeBuilder<HotelMatchDelivery> builder)
    {
        builder.ToTable("HotelMatchDelivery", "dbo");
        builder.HasKey(x => x.Id).HasName("PK_HotelMatchDelivery");
        builder.Property(x => x.SHotelCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MTHotelCode).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.NormalizedDataHash).HasMaxLength(32).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.SupId, x.SHotelCode, x.MatchingVersion });
        builder.HasIndex(x => new { x.DeliveryStatus, x.NextRetryTime });
    }
}

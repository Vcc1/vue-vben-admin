using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys;

public sealed class OtaRoomAiDecision : IHasEntityConfiguration<OtaRoomAiDecision>
{
    public long Id { get; set; }
    public int SupId { get; set; }
    public string SHotelCode { get; set; } = string.Empty;
    public string SRoomCode { get; set; } = string.Empty;
    public string MTHotelCode { get; set; } = string.Empty;
    public string? SourceRoomNameChinese { get; set; }
    public string? SourceRoomNameEnglish { get; set; }
    public int? SearchResultVersion { get; set; }
    public string ProcessorVersion { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public bool SuitableForCreation { get; set; }
    public string? CleanedRoomNameChinese { get; set; }
    public string? CleanedRoomNameEnglish { get; set; }
    public string? CleanedRoomNameLocal { get; set; }
    public string? DecisionJson { get; set; }
    public string ReasonChinese { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public static void Configure(EntityTypeBuilder<OtaRoomAiDecision> builder)
    {
        builder.ToTable("OTARoomAiDecision");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.SHotelCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SRoomCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MTHotelCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProcessorVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PromptVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReasonChinese).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("GETDATE()").IsRequired();
        builder.HasIndex(x => new { x.SupId, x.SHotelCode, x.SRoomCode, x.MTHotelCode }).IsUnique();
    }
}

public sealed class OtaRoomAiCreateFailure : IHasEntityConfiguration<OtaRoomAiCreateFailure>
{
    public long Id { get; set; }
    public int SupId { get; set; }
    public string SHotelCode { get; set; } = string.Empty;
    public string SRoomCode { get; set; } = string.Empty;
    public string MTHotelCode { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public int? SearchResultVersion { get; set; }
    public string ProcessorVersion { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public static void Configure(EntityTypeBuilder<OtaRoomAiCreateFailure> builder)
    {
        builder.ToTable("OTARoomAiCreateFailure");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.SHotelCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SRoomCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MTHotelCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProcessorVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PromptVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("GETDATE()").IsRequired();
        builder.HasIndex(x => new { x.SupId, x.SHotelCode, x.SRoomCode, x.MTHotelCode }).IsUnique();
    }
}

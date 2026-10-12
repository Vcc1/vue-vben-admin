using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OTADomain.RoomDataProcessings;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys;

[Table("QueryPriceSupRoomBuildTask")]
public sealed class QueryPriceSupRoomBuildTask : IHasEntityConfiguration<QueryPriceSupRoomBuildTask>
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string MTHotelCode { get; set; } = "";

    public QueryPriceSupRoomBuildTaskStatus Status { get; set; } = QueryPriceSupRoomBuildTaskStatus.Pending;

    public bool IsRoomBuilt { get; set; }

    [StringLength(200)]
    public string? TargetSuppliers { get; set; }

    public DateTime? LastTriggeredAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? LastPriceHitAt { get; set; }

    public DateTime? LastQueuedAt { get; set; }

    public DateTime? LastCompletedAt { get; set; }

    public int AttemptCount { get; set; }

    [StringLength(2000)]
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public static void Configure(EntityTypeBuilder<QueryPriceSupRoomBuildTask> builder)
    {
        builder.ToTable("QueryPriceSupRoomBuildTask");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.MTHotelCode).IsUnique();
        builder.Property(e => e.MTHotelCode).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.TargetSuppliers).HasMaxLength(200);
        builder.Property(e => e.LastError).HasMaxLength(2000);
    }
}

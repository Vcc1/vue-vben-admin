using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Infrastructure.Sql.Components;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public sealed class SupCachePriceDailyRecord : IHasEntityConfiguration<SupCachePriceDailyRecord>
{
    public int Id { get; set; }
    public int SupId { get; set; }
    public string SupFlag { get; set; } = null!;
    public int BeginPriceDay { get; set; }
    public int EndPriceDay { get; set; }
    public int StayDays { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public int PriceDay { get; set; }
    public DateTime CheckIn { get; set; }
    public int ElapsedMinute { get; set; }
    public int TotalHotel { get; set; }
    public int PriceHotel { get; set; }
    public int InQueueHotel { get; set; }
    public DateTime CreateTime { get; set; }
    public string CreateBy { get; set; } = null!;
    public DateTime LastUpdateTime { get; set; }
    public string LastUpdateBy { get; set; } = null!;
    public bool IsDeleted { get; set; }

    public static void Configure(EntityTypeBuilder<SupCachePriceDailyRecord> b)
    {
        b.ToTable("SupCachePriceDailyRecord");
        b.HasKey(x => x.Id).HasName("PK_SupCachePriceDailyRecord");
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.SupId).IsRequired();
        b.Property(x => x.SupFlag).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.BeginPriceDay).IsRequired();
        b.Property(x => x.EndPriceDay).IsRequired();
        b.Property(x => x.StayDays).IsRequired();
        b.Property(x => x.Adults).IsRequired();
        b.Property(x => x.Children).IsRequired();
        b.Property(x => x.PriceDay).IsRequired();
        b.Property(x => x.CheckIn).HasColumnType("datetime");
        b.Property(x => x.ElapsedMinute).IsRequired();
        b.Property(x => x.TotalHotel).IsRequired();
        b.Property(x => x.PriceHotel).IsRequired();
        b.Property(x => x.InQueueHotel).IsRequired();
        b.Property(x => x.CreateTime).HasColumnType("datetime");
        b.Property(x => x.CreateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.LastUpdateTime).HasColumnType("datetime");
        b.Property(x => x.LastUpdateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.IsDeleted).IsRequired();
    }
}

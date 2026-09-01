using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Infrastructure.Sql.Components;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

/// <summary>
/// 每次运行结果：按酒店/供应商一条记录，展开分段与价格区间。
/// </summary>
public sealed class HotelSupplierStat : IHasEntityConfiguration<HotelSupplierStat>
{
    public int Id { get; set; }
    public string MTHotelCode { get; set; } = null!;
    public int SupId { get; set; }

    // 元信息
    public DateTime CreateTime { get; set; }

    // Segment 7
    public decimal Seg7_LowPriceRatio { get; set; }
    public decimal Seg7_AvgPriceDiffRate { get; set; }
    public decimal Seg7_AvgContinuousRate { get; set; }
    public int Seg7_RoomCoverage { get; set; }
    public int Seg7_TotalPrices { get; set; }
    public int Seg7_LowPriceCount { get; set; }
    public int Seg7_WindowPriceCount { get; set; }
    public decimal Seg7_PriceCoverageRatio { get; set; }
    public decimal Seg7_AdvantageScore { get; set; }
    // price bands
    public int Seg7_Band_0_300_PriceCount { get; set; }
    public int Seg7_Band_0_300_LowCount { get; set; }
    public int Seg7_Band_300_600_PriceCount { get; set; }
    public int Seg7_Band_300_600_LowCount { get; set; }
    public int Seg7_Band_600_1200_PriceCount { get; set; }
    public int Seg7_Band_600_1200_LowCount { get; set; }
    public int Seg7_Band_1200_P_PriceCount { get; set; }
    public int Seg7_Band_1200_P_LowCount { get; set; }

    // Segment 30
    public decimal Seg30_LowPriceRatio { get; set; }
    public decimal Seg30_AvgPriceDiffRate { get; set; }
    public decimal Seg30_AvgContinuousRate { get; set; }
    public int Seg30_RoomCoverage { get; set; }
    public int Seg30_TotalPrices { get; set; }
    public int Seg30_LowPriceCount { get; set; }
    public int Seg30_WindowPriceCount { get; set; }
    public decimal Seg30_PriceCoverageRatio { get; set; }
    public decimal Seg30_AdvantageScore { get; set; }
    // price bands
    public int Seg30_Band_0_300_PriceCount { get; set; }
    public int Seg30_Band_0_300_LowCount { get; set; }
    public int Seg30_Band_300_600_PriceCount { get; set; }
    public int Seg30_Band_300_600_LowCount { get; set; }
    public int Seg30_Band_600_1200_PriceCount { get; set; }
    public int Seg30_Band_600_1200_LowCount { get; set; }
    public int Seg30_Band_1200_P_PriceCount { get; set; }
    public int Seg30_Band_1200_P_LowCount { get; set; }

    // Segment 60
    public decimal Seg60_LowPriceRatio { get; set; }
    public decimal Seg60_AvgPriceDiffRate { get; set; }
    public decimal Seg60_AvgContinuousRate { get; set; }
    public int Seg60_RoomCoverage { get; set; }
    public int Seg60_TotalPrices { get; set; }
    public int Seg60_LowPriceCount { get; set; }
    public int Seg60_WindowPriceCount { get; set; }
    public decimal Seg60_PriceCoverageRatio { get; set; }
    public decimal Seg60_AdvantageScore { get; set; }
    // price bands
    public int Seg60_Band_0_300_PriceCount { get; set; }
    public int Seg60_Band_0_300_LowCount { get; set; }
    public int Seg60_Band_300_600_PriceCount { get; set; }
    public int Seg60_Band_300_600_LowCount { get; set; }
    public int Seg60_Band_600_1200_PriceCount { get; set; }
    public int Seg60_Band_600_1200_LowCount { get; set; }
    public int Seg60_Band_1200_P_PriceCount { get; set; }
    public int Seg60_Band_1200_P_LowCount { get; set; }

    // Segment 180
    public decimal Seg180_LowPriceRatio { get; set; }
    public decimal Seg180_AvgPriceDiffRate { get; set; }
    public decimal Seg180_AvgContinuousRate { get; set; }
    public int Seg180_RoomCoverage { get; set; }
    public int Seg180_TotalPrices { get; set; }
    public int Seg180_LowPriceCount { get; set; }
    public int Seg180_WindowPriceCount { get; set; }
    public decimal Seg180_PriceCoverageRatio { get; set; }
    public decimal Seg180_AdvantageScore { get; set; }
    // price bands
    public int Seg180_Band_0_300_PriceCount { get; set; }
    public int Seg180_Band_0_300_LowCount { get; set; }
    public int Seg180_Band_300_600_PriceCount { get; set; }
    public int Seg180_Band_300_600_LowCount { get; set; }
    public int Seg180_Band_600_1200_PriceCount { get; set; }
    public int Seg180_Band_600_1200_LowCount { get; set; }
    public int Seg180_Band_1200_P_PriceCount { get; set; }
    public int Seg180_Band_1200_P_LowCount { get; set; }

    public static void Configure(EntityTypeBuilder<HotelSupplierStat> b)
    {
        b.ToTable("HotelSupplierStat");
        b.HasKey(x => x.Id).HasName("PK_HotelSupplierStat");
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.MTHotelCode).IsRequired().HasMaxLength(64).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.SupId).IsRequired();

        // decimal precisions for ratios/scores
        b.Property(x => x.Seg7_LowPriceRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg7_AvgPriceDiffRate).HasPrecision(18, 4);
        b.Property(x => x.Seg7_AvgContinuousRate).HasPrecision(18, 4);
        b.Property(x => x.Seg7_PriceCoverageRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg7_AdvantageScore).HasPrecision(18, 4);

        b.Property(x => x.Seg30_LowPriceRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg30_AvgPriceDiffRate).HasPrecision(18, 4);
        b.Property(x => x.Seg30_AvgContinuousRate).HasPrecision(18, 4);
        b.Property(x => x.Seg30_PriceCoverageRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg30_AdvantageScore).HasPrecision(18, 4);

        b.Property(x => x.Seg60_LowPriceRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg60_AvgPriceDiffRate).HasPrecision(18, 4);
        b.Property(x => x.Seg60_AvgContinuousRate).HasPrecision(18, 4);
        b.Property(x => x.Seg60_PriceCoverageRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg60_AdvantageScore).HasPrecision(18, 4);

        b.Property(x => x.Seg180_LowPriceRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg180_AvgPriceDiffRate).HasPrecision(18, 4);
        b.Property(x => x.Seg180_AvgContinuousRate).HasPrecision(18, 4);
        b.Property(x => x.Seg180_PriceCoverageRatio).HasPrecision(18, 4);
        b.Property(x => x.Seg180_AdvantageScore).HasPrecision(18, 4);

        b.Property(x => x.CreateTime).HasColumnType("datetime");

        b.HasIndex(x => new { x.MTHotelCode, x.SupId, x.CreateTime }).HasDatabaseName("IX_HotelSupplierStat_Hotel_Sup_Run");
    }
}

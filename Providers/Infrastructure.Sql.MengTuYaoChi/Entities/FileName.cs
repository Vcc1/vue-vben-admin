using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public class HotelSupplierDailySummary : IHasEntityConfiguration<HotelSupplierDailySummary>
{
    public HotelSupplierDailySummary()
    {
    }

    public DateTime StatDate { get; set; }
    public int SupId { get; set; }

    // ===== Seg7 =====
    public decimal Seg7_LowPriceRatio { get; set; }
    public decimal Seg7_AvgPriceDiffRate { get; set; }
    public decimal Seg7_AvgContinuousRate { get; set; }
    public int Seg7_RoomCoverage { get; set; }
    public int Seg7_TotalPrices { get; set; }
    public int Seg7_LowPriceCount { get; set; }
    public int Seg7_WindowPriceCount { get; set; }
    public decimal Seg7_PriceCoverageRatio { get; set; }
    public decimal Seg7_AdvantageScore { get; set; }
    public int Seg7_Band_0_300_PriceCount { get; set; }
    public int Seg7_Band_0_300_LowCount { get; set; }
    public int Seg7_Band_300_600_PriceCount { get; set; }
    public int Seg7_Band_300_600_LowCount { get; set; }
    public int Seg7_Band_600_1200_PriceCount { get; set; }
    public int Seg7_Band_600_1200_LowCount { get; set; }
    public int Seg7_Band_1200_P_PriceCount { get; set; }
    public int Seg7_Band_1200_P_LowCount { get; set; }

    // ===== Seg30 =====
    public decimal Seg30_LowPriceRatio { get; set; }
    public decimal Seg30_AvgPriceDiffRate { get; set; }
    public decimal Seg30_AvgContinuousRate { get; set; }
    public int Seg30_RoomCoverage { get; set; }
    public int Seg30_TotalPrices { get; set; }
    public int Seg30_LowPriceCount { get; set; }
    public int Seg30_WindowPriceCount { get; set; }
    public decimal Seg30_PriceCoverageRatio { get; set; }
    public decimal Seg30_AdvantageScore { get; set; }
    public int Seg30_Band_0_300_PriceCount { get; set; }
    public int Seg30_Band_0_300_LowCount { get; set; }
    public int Seg30_Band_300_600_PriceCount { get; set; }
    public int Seg30_Band_300_600_LowCount { get; set; }
    public int Seg30_Band_600_1200_PriceCount { get; set; }
    public int Seg30_Band_600_1200_LowCount { get; set; }
    public int Seg30_Band_1200_P_PriceCount { get; set; }
    public int Seg30_Band_1200_P_LowCount { get; set; }

    // ===== Seg60 =====
    public decimal Seg60_LowPriceRatio { get; set; }
    public decimal Seg60_AvgPriceDiffRate { get; set; }
    public decimal Seg60_AvgContinuousRate { get; set; }
    public int Seg60_RoomCoverage { get; set; }
    public int Seg60_TotalPrices { get; set; }
    public int Seg60_LowPriceCount { get; set; }
    public int Seg60_WindowPriceCount { get; set; }
    public decimal Seg60_PriceCoverageRatio { get; set; }
    public decimal Seg60_AdvantageScore { get; set; }
    public int Seg60_Band_0_300_PriceCount { get; set; }
    public int Seg60_Band_0_300_LowCount { get; set; }
    public int Seg60_Band_300_600_PriceCount { get; set; }
    public int Seg60_Band_300_600_LowCount { get; set; }
    public int Seg60_Band_600_1200_PriceCount { get; set; }
    public int Seg60_Band_600_1200_LowCount { get; set; }
    public int Seg60_Band_1200_P_PriceCount { get; set; }
    public int Seg60_Band_1200_P_LowCount { get; set; }

    // ===== Seg180 =====
    public decimal Seg180_LowPriceRatio { get; set; }
    public decimal Seg180_AvgPriceDiffRate { get; set; }
    public decimal Seg180_AvgContinuousRate { get; set; }
    public int Seg180_RoomCoverage { get; set; }
    public int Seg180_TotalPrices { get; set; }
    public int Seg180_LowPriceCount { get; set; }
    public int Seg180_WindowPriceCount { get; set; }
    public decimal Seg180_PriceCoverageRatio { get; set; }
    public decimal Seg180_AdvantageScore { get; set; }
    public int Seg180_Band_0_300_PriceCount { get; set; }
    public int Seg180_Band_0_300_LowCount { get; set; }
    public int Seg180_Band_300_600_PriceCount { get; set; }
    public int Seg180_Band_300_600_LowCount { get; set; }
    public int Seg180_Band_600_1200_PriceCount { get; set; }
    public int Seg180_Band_600_1200_LowCount { get; set; }
    public int Seg180_Band_1200_P_PriceCount { get; set; }
    public int Seg180_Band_1200_P_LowCount { get; set; }

    // ===== 有价酒店数 & 总酒店数 =====
    public int Seg7_AvailableHotelCount { get; set; }
    public int Seg30_AvailableHotelCount { get; set; }
    public int Seg60_AvailableHotelCount { get; set; }
    public int Seg180_AvailableHotelCount { get; set; }
    public int All_AvailableHotelCount { get; set; }
    public int HotelSettingTotalCount { get; set; }
    public int HotelMapTotalCount { get; set; }



    public static void Configure(EntityTypeBuilder<HotelSupplierDailySummary> b)
    {
        b.ToTable("HotelSupplierDailySummary", "dbo");
        // 复合主键
        b.HasKey(x => new { x.StatDate, x.SupId });
        // 二级索引
        b.HasIndex(x => new { x.SupId, x.StatDate })
         .HasDatabaseName("IX_HotelSupplierDailySummary_SupId_StatDate");
        // 列类型
        b.Property(x => x.StatDate).HasColumnType("date");
        // decimal 精度（18,6）——比率/分数类
        b.Property(x => x.Seg7_LowPriceRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg7_AvgPriceDiffRate).HasPrecision(18, 6);
        b.Property(x => x.Seg7_AvgContinuousRate).HasPrecision(18, 6);
        b.Property(x => x.Seg7_PriceCoverageRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg7_AdvantageScore).HasPrecision(18, 6);

        b.Property(x => x.Seg30_LowPriceRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg30_AvgPriceDiffRate).HasPrecision(18, 6);
        b.Property(x => x.Seg30_AvgContinuousRate).HasPrecision(18, 6);
        b.Property(x => x.Seg30_PriceCoverageRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg30_AdvantageScore).HasPrecision(18, 6);

        b.Property(x => x.Seg60_LowPriceRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg60_AvgPriceDiffRate).HasPrecision(18, 6);
        b.Property(x => x.Seg60_AvgContinuousRate).HasPrecision(18, 6);
        b.Property(x => x.Seg60_PriceCoverageRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg60_AdvantageScore).HasPrecision(18, 6);

        b.Property(x => x.Seg180_LowPriceRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg180_AvgPriceDiffRate).HasPrecision(18, 6);
        b.Property(x => x.Seg180_AvgContinuousRate).HasPrecision(18, 6);
        b.Property(x => x.Seg180_PriceCoverageRatio).HasPrecision(18, 6);
        b.Property(x => x.Seg180_AdvantageScore).HasPrecision(18, 6);

        // 其余整型字段默认映射即可（NOT NULL）
    }
}






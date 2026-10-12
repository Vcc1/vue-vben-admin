using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public sealed class HotelPriceComparisonRunHistory : IHasEntityConfiguration<HotelPriceComparisonRunHistory>
{
    public long HistoryId { get; set; }
    public Guid RunId { get; set; }
    public DateTime RunStartTime { get; set; }
    public DateTime RunEndTime { get; set; }
    public DateOnly StatDate { get; set; }

    public long RecordCount { get; set; }
    public long HotelCount { get; set; }
    public long ErrorRecordCount { get; set; }

    public long EanepsPairCount { get; set; }
    public long EanepsHigherPriceCount { get; set; }
    public long EanepsFlatPriceCount { get; set; }
    public long EanepsAdvantagePriceCount { get; set; }
    public decimal? EpsHigherPct { get; set; }
    public decimal? EpsFlatPct { get; set; }
    public decimal? EpsAdvantagePct { get; set; }

    public long EanbizPairCount { get; set; }
    public long EanbizHigherPriceCount { get; set; }
    public long EanbizFlatPriceCount { get; set; }
    public long EanbizAdvantagePriceCount { get; set; }
    public decimal? BizHigherPct { get; set; }
    public decimal? BizFlatPct { get; set; }
    public decimal? BizAdvantagePct { get; set; }

    public long AccountPairCount { get; set; }
    public long AccountHigherCount { get; set; }
    public long AccountFlatCount { get; set; }
    public long AccountAdvantageCount { get; set; }
    public decimal? AccountEpsHigherPct { get; set; }
    public decimal? AccountFlatPct { get; set; }
    public decimal? AccountEpsAdvantagePct { get; set; }

    public DateTime InsertedTime { get; set; }

    public static void Configure(EntityTypeBuilder<HotelPriceComparisonRunHistory> b)
    {
        b.ToTable("HotelPriceComparisonRunHistory");
        b.HasKey(x => x.HistoryId).HasName("PK_HotelPriceComparisonRunHistory");
        b.Property(x => x.HistoryId).ValueGeneratedOnAdd();

        b.Property(x => x.RunId).IsRequired();
        b.Property(x => x.RunStartTime).HasColumnType("datetime2(0)").IsRequired();
        b.Property(x => x.RunEndTime).HasColumnType("datetime2(0)").IsRequired();
        b.Property(x => x.StatDate).IsRequired();

        b.Property(x => x.RecordCount).IsRequired();
        b.Property(x => x.HotelCount).IsRequired();
        b.Property(x => x.ErrorRecordCount).IsRequired();

        b.Property(x => x.EanepsPairCount).IsRequired();
        b.Property(x => x.EanepsHigherPriceCount).IsRequired();
        b.Property(x => x.EanepsFlatPriceCount).IsRequired();
        b.Property(x => x.EanepsAdvantagePriceCount).IsRequired();
        b.Property(x => x.EpsHigherPct).HasPrecision(10, 2);
        b.Property(x => x.EpsFlatPct).HasPrecision(10, 2);
        b.Property(x => x.EpsAdvantagePct).HasPrecision(10, 2);

        b.Property(x => x.EanbizPairCount).IsRequired();
        b.Property(x => x.EanbizHigherPriceCount).IsRequired();
        b.Property(x => x.EanbizFlatPriceCount).IsRequired();
        b.Property(x => x.EanbizAdvantagePriceCount).IsRequired();
        b.Property(x => x.BizHigherPct).HasPrecision(10, 2);
        b.Property(x => x.BizFlatPct).HasPrecision(10, 2);
        b.Property(x => x.BizAdvantagePct).HasPrecision(10, 2);

        b.Property(x => x.AccountPairCount).IsRequired();
        b.Property(x => x.AccountHigherCount).IsRequired();
        b.Property(x => x.AccountFlatCount).IsRequired();
        b.Property(x => x.AccountAdvantageCount).IsRequired();
        b.Property(x => x.AccountEpsHigherPct).HasPrecision(10, 2);
        b.Property(x => x.AccountFlatPct).HasPrecision(10, 2);
        b.Property(x => x.AccountEpsAdvantagePct).HasPrecision(10, 2);

        b.Property(x => x.InsertedTime)
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSDATETIME()")
            .IsRequired();

        b.HasIndex(x => x.RunId)
            .IsUnique()
            .HasDatabaseName("UX_HotelPriceComparisonRunHistory_RunId");
        b.HasIndex(x => x.StatDate)
            .HasDatabaseName("IX_HotelPriceComparisonRunHistory_StatDate");
    }
}

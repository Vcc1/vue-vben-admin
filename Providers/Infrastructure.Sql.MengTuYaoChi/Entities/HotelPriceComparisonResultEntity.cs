using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public sealed class HotelPriceComparisonResultEntity : IHasEntityConfiguration<HotelPriceComparisonResultEntity>
{
    public long Id { get; set; }
    public Guid RunId { get; set; }
    public DateOnly StatDate { get; set; }
    public string HotelCode { get; set; } = null!;
    public int EanepsPriceCount { get; set; }
    public int EanbizPriceCount { get; set; }
    public int EanepsPackagePriceCount { get; set; }
    public int EanbizPackagePriceCount { get; set; }
    public int EanepsStandardPriceCount { get; set; }
    public int EanbizStandardPriceCount { get; set; }
    public int EanepsPairCount { get; set; }
    public int EanepsAdvantagePriceCount { get; set; }
    public int EanepsHigherPriceCount { get; set; }
    public int EanepsFlatPriceCount { get; set; }
    public int EanepsStandardHasPricePackageMissingCount { get; set; }
    public int EanepsStandardMissingPackageHasPriceCount { get; set; }
    public int EanbizPairCount { get; set; }
    public int EanbizAdvantagePriceCount { get; set; }
    public int EanbizHigherPriceCount { get; set; }
    public int EanbizFlatPriceCount { get; set; }
    public int EanbizStandardHasPricePackageMissingCount { get; set; }
    public int EanbizStandardMissingPackageHasPriceCount { get; set; }
    public int AccountPairCount { get; set; }
    public int EanepsAdvantagePriceCountForAccountComparison { get; set; }
    public int EanepsHigherPriceCountForAccountComparison { get; set; }
    public int EanepsFlatPriceCountForAccountComparison { get; set; }
    public int EanepsHasPriceEanbizMissingCount { get; set; }
    public int EanepsMissingEanbizHasPriceCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreateTime { get; set; }

    public static void Configure(EntityTypeBuilder<HotelPriceComparisonResultEntity> b)
    {
        b.ToTable("HotelPriceComparisonResult");
        b.HasKey(x => x.Id).HasName("PK_HotelPriceComparisonResult");
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.HotelCode).IsRequired().HasMaxLength(64).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.ErrorMessage).HasMaxLength(1000).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.CreateTime).HasColumnType("datetime");
        b.HasIndex(x => new { x.RunId, x.HotelCode }).HasDatabaseName("IX_HotelPriceComparisonResult_Run_Hotel");
        b.HasIndex(x => x.StatDate).HasDatabaseName("IX_HotelPriceComparisonResult_StatDate");
    }
}

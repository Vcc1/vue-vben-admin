using Infrastructure.Sql.Components;
using Infrastructure.Sql.MengTuYaoChi.Entities;
using Microsoft.EntityFrameworkCore;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.MengTuYaoChi;

[EFDbService("MengtuYaoChi")]
public class MengtuYaoChiDbContext : DbContext
{
    public MengtuYaoChiDbContext(DbContextOptions<MengtuYaoChiDbContext> options) : base(options)
    {
    }

    protected MengtuYaoChiDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<HotelSetting> HotelSettings { get; set; } = null!;
    public DbSet<SupCachePriceDailyRecord> SupCachePriceDailyRecords { get; set; } = null!;
    public DbSet<SupCachePriceRecord> SupCachePriceRecords { get; set; } = null!;
    public DbSet<SupplierSetting> SupplierSettings { get; set; } = null!;
    public DbSet<CachePlanSetting> CachePlanSettings { get; set; } = null!;
    public DbSet<HotelSupplierStat> HotelSupplierStats { get; set; } = null!;
    public DbSet<HotelPriceComparisonResultEntity> HotelPriceComparisonResults { get; set; } = null!;
    public DbSet<HotelPriceComparisonRunHistory> HotelPriceComparisonRunHistories { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEntityConfigsFromModel(this);
    }
}

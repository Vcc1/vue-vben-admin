using Microsoft.EntityFrameworkCore;
using Infrastructure.Sql.MengTuYDOTA.Entitys;
using Infrastructure.Sql.MengTuYDOTA.Entitys.Views;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.MengTuYDOTA;


[EFDbService("MT_YD_OTA")]
public class MengTuYDOTAContext : DbContext
{
    public MengTuYDOTAContext(DbContextOptions<MengTuYDOTAContext> options) : base(options)
    {


    }
    protected MengTuYDOTAContext(DbContextOptions options) : base(options)
    {

    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);//全局默认禁止跟踪 需要启用使用 AsTracking
    }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.
        //modelBuilder.Entity<PlatformHotelSaleRule>().Property(t => t.CreateTime).HasColumnType("datetime");
        //modelBuilder.Entity<PlatformHotelSaleRule>().Property(t => t.LastUpdateTime).HasColumnType("datetime");


        modelBuilder.Entity<AgodaRoomInfoMapping>().HasNoKey();
        modelBuilder.Entity<匹配关系数据导入156>().HasNoKey();
        modelBuilder.Entity<匹配关系数据导入166>().HasNoKey();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.DefaultTypeMapping<匹配关系数据导入156>();
        configurationBuilder.DefaultTypeMapping<匹配关系数据导入166>();
    }
    public DbSet<MTCountry> MTCountries { get; set; }
    public DbSet<AgodaHotelInfo> AgodaHotelInfos { get; set; }
    public DbSet<AutoBookingOrder> AutoBookingOrders { get; set; }
    public DbSet<OTAHotel> RecordHotelIsPrices { get; set; }
    public DbSet<AgodaCountry> AgodaCountrys { get; set; }
    public DbSet<AgodaHotelPlatFormRoomInfo> AgodaHotelPlatFormRoomInfos { get; set; }
    public DbSet<AgodaHotelRoomInfo> AgodaHotelRoomInfos { get; set; }
    public DbSet<SubRoomPricePlan> SubRoomPricePlans { get; set; }
    public DbSet<AgodaHotelHighRoomFilter> AgodaHotelHighRoomFilters { get; set; }
    public DbSet<PlatformHotelSaleRule> PlatformHotelSaleRules { get; set; }
    public DbSet<PlatformHotelSaleRuleLog> PlatformHotelSaleRuleLogs { get; set; }
    public DbSet<CtripHotelRoomInfo> CtripHotelRoomInfos { get; set; }
    public DbSet<NewCtripHotelRoomInfo> NewCtripHotelRoomInfos { get; set; }
    public DbSet<NewCtripHotelInfo> NewCtripHotelInfos { get; set; }
    public DbSet<AgodaHotelMateRoomInfo> AgodaHotelMateRoomInfos { get; set; }
    public DbSet<AgodaHotelMateRoomInfoRelationship> AgodaHotelMateRoomInfoRelationships { get; set; }
    public DbSet<TaskReplaceMTHotelCode> TaskReplaceMTHotelCodes { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<AgodaRoomInfoMapping> AgodaRoomInfoMappings { get; set; }

    public DbSet<OTANoMappingSaleHotel> OTANoMappingSaleHotels { get; set; }
    //public DbSet<Auto> AgodaRoomInfoMappings { get; set; }


    #region 报表数据
    public DbSet<StatisticsHotelPrice> StatisticsHotelPrices { get; set; }

    #endregion

    #region 临时导入数据
    public DbSet<匹配关系数据导入156> 匹配关系数据导入156s { get; set; }
    public DbSet<匹配关系数据导入166> 匹配关系数据导入166s { get; set; }

    #endregion
}


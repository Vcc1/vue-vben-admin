using Infrastructure.Sql.Components;
using Infrastructure.Sql.MengTuSup.Entitys;
//using Infrastructure.Sql.MengTuYDOTA.Entitys;
//using Infrastructure.Sql.MengTuYDOTA.Entitys.Views;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using System;
using System.Reflection.Emit;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.RoomMapping;



[EFDbService("RoomMapping")]
public class RoomMappingDbContext : DbContext
{
    public RoomMappingDbContext(DbContextOptions<RoomMappingDbContext> options) : base(options)
    {
    }
    protected RoomMappingDbContext(DbContextOptions options) : base(options)
    {

    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);//全局默认禁止跟踪 需要启用使用 AsTracking
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEntityConfigsFromModel(this);

        modelBuilder.Entity<OTARoomInfo>()
            .HasOne(e => e.Extension)
            .WithOne(e => e.OTARoomInfo)
            .HasForeignKey<OTARoomInfoExtension>(e => e.Id);

        modelBuilder.Entity<OTARoomInfoExtension>()
            .HasMany(e => e.Logs)
            .WithOne(e => e.OTARoomInfoExtension)
            .HasForeignKey(e => e.OTARoomInfoExtensionId);

        modelBuilder.Entity<OTARoomInfoExtension>()
            .HasIndex(e => e.MTHotelCode);

        modelBuilder.Entity<OTARoomInfoExtensionLog>()
            .HasIndex(e => e.MTHotelCode);
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

    }

    public DbSet<OTARoomInfo> OTARoomInfos { get; set; }
    public DbSet<SupRoomInfo> SupRoomInfos { get; set; }
    /// <summary>
    /// API插入 写数据库
    /// </summary>
    public DbSet<TempSupRoomInfo> TempSupRoomInfos { get; set; }
    /// <summary>
    /// 服务插入 写数据库 各自独立防止冲突
    /// </summary>
    public DbSet<TempSupRoomInfo2> TempSupRoomInfo2s { get; set; }
    public DbSet<RoomMap> RoomMaps { get; set; }


    public DbSet<WordList> WordLists { get; set; }
    public DbSet<WordType> WordTypes { get; set; }
    public DbSet<OTARoomInfoPre> OTARoomInfoPres { get; set; }
    public DbSet<OTARoomInfoExtension> OTARoomInfoExtensions { get; set; }
    public DbSet<OTARoomInfoExtensionLog> OTARoomInfoExtensionLogs { get; set; }

    public DbSet<NormUnknownChar> NormUnknownChars { get; set; }

    public DbSet<SearchHasPriceHotelTask> SearchHasPriceHotelTasks { get; set; }
    public DbSet<QueryPriceSupRoomBuildTask> QueryPriceSupRoomBuildTasks { get; set; }
    public DbSet<SupRoomInfoStat> SupRoomInfoStats { get; set; }

    public DbSet<SupRoomInfoView> SupRoomInfoView { get; set; }

    public DbSet<SupHotelInfoView> SupHotelInfoViews { get; set; }
    public DbSet<OTAHotelInfoRaw> OTAHotelInfoRaws { get; set; }
    public DbSet<OTAHotelInfo> OTAHotelInfos { get; set; }
    public DbSet<SupHotelInfoRaw> SupHotelInfoRaws { get; set; }
    public DbSet<SupHotelInfoClean> SupHotelInfos { get; set; }
    public DbSet<HotelMatchDelivery> HotelMatchDeliveries { get; set; }
    public DbSet<OTAHotelPhone> OTAHotelPhones { get; set; }
    public DbSet<SupHotelPhone> SupHotelPhones { get; set; }
    public DbSet<HotelStarLevelIssue> HotelStarLevelIssues { get; set; }
    public DbSet<HotelCleaningFailure> HotelCleaningFailures { get; set; }
    public DbSet<SupHotelDataSource> SupHotelDataSources { get; set; }
    public DbSet<SupStaticRoomInfo> SupStaticRoomInfos { get; set; }
    public DbSet<AgodaHotelRoomSource> AgodaHotelRoomSources { get; set; }
    public DbSet<AgodaRoomMapSource> AgodaRoomMapSources { get; set; }
    public DbSet<AgodaSupRoomMatchResult> AgodaSupRoomMatchResults { get; set; }
    public DbSet<SqllabSuperaggRoomMappingSt> SqllabSuperaggRoomMappingSts { get; set; }
}



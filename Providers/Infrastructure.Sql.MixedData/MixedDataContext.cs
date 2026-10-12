using Microsoft.EntityFrameworkCore;
//using Infrastructure.Sql.MengTuYDOTA.Entitys;
//using Infrastructure.Sql.MengTuYDOTA.Entitys.Views;
using Infrastructure.Sql.MengTuSup.Entitys;
using VBaseFramework.Core.EF;
using Infrastructure.Sql.MixedData.Entitys;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Sql.MixedData;

[EFDbService("MixedData")]
public class MixedDataContext : DbContext
{
    public MixedDataContext(DbContextOptions<MixedDataContext> options) : base(options)
    {

    }

    protected MixedDataContext(DbContextOptions options) : base(options)
    {

    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);//全局默认禁止跟踪 需要启用使用 AsTracking
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SupRoomInfoView>(entity =>
        {
            entity.HasNoKey();  // 视图通常没有主键（或者你可以手动设置）
            entity.ToView("V_SupRoomInfo");  // 映射到视图名
        });

        modelBuilder.Entity<EANHotel>(entity =>
        {
            entity.HasNoKey();  // 视图通常没有主键（或者你可以手动设置）
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
    }

    public DbSet<SupHotelDataSource> SupHotelDataSources { get; set; }
    public DbSet<SupRoomInfo> SupRoomInfo { get; set; }

    //public DbSet<WordList> WordLists { get; set; }
    //public DbSet<WordType> WordTypes { get; set; }

    //public DbSet<NoSymbolAggressiveWordList> NoSymbolAggressiveWordLists { get; set; }
    //public DbSet<NoSymbolAggressiveWordType> NoSymbolAggressiveWordTypes { get; set; }

    public DbSet<WordRepeatStat> WordRepeatStats { get; set; }
    //public DbSet<WordFrequency> WordFrequencys { get; set; }

    //public DbSet<RoomMappingCtrip> RoomMappingCtrips { get; set; }


    //public DbSet<EBKRoomMapping> EBKRoomMappings { get; set; }
    public DbSet<RoomMappingRedisModel> RoomMappingRedisModels { get; set; }

    //public DbSet<RoomMatching> RoomMatchings { get; set; }

    public DbSet<EANHotel> EANHotels { get; set; }
    public DbSet<QueryPriceContrast> QueryPriceContrasts { get; set; }

}

[Table("RoomMappingRedisModel")]
public class RoomMappingRedisModel
{
    [Key]
    public long Id { get; set; } // 👈 必须加主键！

    public string MTHotelCode { get; internal set; }
    /// <summary>
    /// 供应商房型名称（中文或英文）
    /// </summary>
    public string SupRoomName { get; set; }

    /// <summary>
    /// 供应商床型键
    /// </summary>
    public string SupBedInfoKey { get; set; }

    /// <summary>
    /// 对应的美团房型ID（OTARoomId）
    /// </summary>
    public long MTRoomId { get; set; }

    /// <summary>
    /// 房型匹配状态
    /// </summary>
    public int MTRoomStatus { get; set; }

    /// <summary>
    /// 房型匹配类型
    /// </summary>
    public int MTRoomType { get; set; }

    /// <summary>
    /// 美团 OTARoomId
    /// </summary>
    public long OTARoomId { get; set; }

    /// <summary>
    /// 美团房型中文名
    /// </summary>
    public string OTARoomNameCN { get; set; }

    /// <summary>
    /// 美团房型英文名
    /// </summary>
    public string OTARoomNameEN { get; set; }

    /// <summary>
    /// 美团床型Key
    /// </summary>
    public string OTABedInfoKey { get; set; }

}



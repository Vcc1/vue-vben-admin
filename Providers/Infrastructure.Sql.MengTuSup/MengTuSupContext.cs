using Microsoft.EntityFrameworkCore;
using Infrastructure.Sql.MengTuSup.Entitys;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.MengTuSup;



[EFDbService("MengTuSup")]


public class MengTuSupContext : DbContext
{
    public MengTuSupContext(DbContextOptions<MengTuSupContext> options) : base(options)
    {


    }
    protected MengTuSupContext(DbContextOptions options) : base(options)
    {

    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);//全局默认禁止跟踪 需要启用使用 AsTracking
    }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
    }
    public DbSet<SupRoomInfo> SupRoomInfos { get; set; }
    //public DbSet<WordList> WordLists { get; set; }
    //public DbSet<WordType> WordTypes { get; set; }
    //public DbSet<ContrastWordList> ContrastWordLists { get; set; }
    //public DbSet<ContrastWordType> ContrastWordTypes { get; set; }
    public DbSet<NoSymbolAggressiveWordList> NoSymbolAggressiveWordLists { get; set; }
    public DbSet<NoSymbolAggressiveWordType> NoSymbolAggressiveWordTypes { get; set; }
    public DbSet<RoomMappingCtrip> RoomMappingCtrips { get; set; }
    public DbSet<WordFrequency> WordFrequencys { get; set; }
    public DbSet<EBKRoomMapping> EBKRoomMappings { get; set; }

    //public DbSet<CtripHotelRoomInfo> CtripHotelRoomInfos { get; set; }
    public DbSet<SupRoomInfoView> SupRoomInfoView { get; set; }
    //public DbSet<SearchHasPriceHotelTask> SearchHasPriceHotelTasks { get; set; }


}


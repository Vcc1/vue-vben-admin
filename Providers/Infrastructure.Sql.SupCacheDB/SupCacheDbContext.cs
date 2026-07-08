using Infrastructure.Sql.SupCacheDB.Entitys;
using Microsoft.EntityFrameworkCore;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.SupCacheDB;

[EFDbService("SupCacheDB")]
public class SupCacheDbContext : DbContext
{
    public SupCacheDbContext(DbContextOptions<SupCacheDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExpediaChaseRoomHotel>()
            .HasIndex(x => new { x.MTHotelCode, x.PropertyId, x.Rooms, x.Adults, x.CheckIn, x.CheckOut })
            .HasDatabaseName("IX_ExpediaChaseRoomHotels_TaskKey");

        modelBuilder.Entity<OffLineHotel>()
            .HasIndex(x => x.MTHotelCode)
            .HasDatabaseName("IX_OffLine_MTHotelCode")
            .IsUnique(false);

        modelBuilder.Entity<ExpediaTaapAccount>()
            .HasIndex(x => x.SigninEmailAddress)
            .HasDatabaseName("IX_ExpediaTaapAccounts_SigninEmailAddress")
            .IsUnique();

        modelBuilder.Entity<ExpediaTaapAccount>()
            .HasIndex(x => new { x.AccountLevelId, x.IsEnabled })
            .HasDatabaseName("IX_ExpediaTaapAccounts_Level_Enabled");

        modelBuilder.Entity<ExpediaTaapAccount>()
            .HasIndex(x => new { x.CanRefreshSharedSha256Hash, x.IsEnabled })
            .HasDatabaseName("IX_ExpediaTaapAccounts_SharedSha_Enabled");

        modelBuilder.Entity<ExpediaTaapAccountLevel>()
            .HasIndex(x => x.LevelCode)
            .HasDatabaseName("IX_ExpediaTaapAccountLevels_LevelCode")
            .IsUnique();

        modelBuilder.Entity<ExpediaTaapAccountLevel>()
            .HasIndex(x => x.LevelName)
            .HasDatabaseName("IX_ExpediaTaapAccountLevels_LevelName")
            .IsUnique();

        modelBuilder.Entity<ExpediaTaapAccountLevel>()
            .HasIndex(x => new { x.SortOrder, x.IsEnabled })
            .HasDatabaseName("IX_ExpediaTaapAccountLevels_Sort_Enabled");

        modelBuilder.Entity<ExpediaTaapAccountLevelCommissionRate>()
            .HasIndex(x => new { x.AccountLevelId, x.ProductCode })
            .HasDatabaseName("IX_ExpediaTaapAccountLevelCommissionRates_Level_Product")
            .IsUnique();

        modelBuilder.Entity<ExpediaTaapAccountLevelCommissionRate>()
            .HasIndex(x => new { x.AccountLevelId, x.SortOrder, x.IsEnabled })
            .HasDatabaseName("IX_ExpediaTaapAccountLevelCommissionRates_Level_Sort_Enabled");
    }

    public DbSet<ExpediaChaseRoomHotel> ExpediaChaseRoomHotels { get; set; }

    public DbSet<ExpediaChaseRoomHotelsLog> ExpediaChaseRoomHotelsLogs { get; set; }

    public DbSet<OffLineHotel> OffLineHotels { get; set; }

    public DbSet<ExpediaTaapAccount> ExpediaTaapAccounts { get; set; }

    public DbSet<ExpediaTaapAccountLevel> ExpediaTaapAccountLevels { get; set; }

    public DbSet<ExpediaTaapAccountLevelCommissionRate> ExpediaTaapAccountLevelCommissionRates { get; set; }
}

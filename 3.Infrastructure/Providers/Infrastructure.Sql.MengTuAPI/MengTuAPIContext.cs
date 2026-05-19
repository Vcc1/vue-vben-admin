using Infrastructures.Sql.MengTuAPI.Entitys;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Diagnostics.Tracing;
using VBaseFramework.Core.EF;

namespace Infrastructure.Sql.MengTuAPI
{
    [EFDbService("MengTuApi")]
    public class MengTuAPIDbContext : DbContext
    {
        public MengTuAPIDbContext(DbContextOptions<MengTuAPIDbContext> options) : base(options)
        {


        }
        protected MengTuAPIDbContext(DbContextOptions options) : base(options)
        {


        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }

        public DbSet<SupHotelInfo> SupHotelInfos { get; set; }
        public DbSet<SupCountryInfo> SupCountryInfos { get; set; }
        public DbSet<RecordHotelIsPrice> RecordHotelIsPrices { get; set; }
        public DbSet<MTSupplier> MTSuppliers { get; set; }
    }
}

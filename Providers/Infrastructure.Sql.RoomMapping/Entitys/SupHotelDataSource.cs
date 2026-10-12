using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VBaseFramework.Core.String;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    public sealed class SupHotelDataSource : IHasEntityConfiguration<SupHotelDataSource>
    {
        public long ID { get; set; }

        public string MTHotelCode { get; set; } = null!;
        public string SHotelCode { get; set; } = null!;
        public int SupId { get; set; }

        // EF 内部会自动压缩/解压，外部就是 string
        public string Data { get; set; } = string.Empty;



        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
        public DateTime DataUpdateTime { get; set; }

        // —— 配置方法（静态） ——
        public static void Configure(EntityTypeBuilder<SupHotelDataSource> b)
        {
            b.ToTable("SupHotelDataSource", "dbo");

            b.HasKey(x => x.ID).HasName("PK_SupHotelDataSource");

            b.Property(x => x.MTHotelCode)
                .IsRequired()
                .HasMaxLength(64)
                .UseCollation("Chinese_PRC_CI_AS");

            b.Property(x => x.SHotelCode)
                .IsRequired()
                .HasMaxLength(64)
                .UseCollation("Chinese_PRC_CI_AS");

            b.Property(x => x.SupId).IsRequired();

            // —— string <-> varbinary(max) 压缩存储 ——
            var converter = new ValueConverter<string, byte[]>(
                toProvider => StringCompression.CompressToBrotli(toProvider),
                fromProvider => StringCompression.DecompressFromBrotli(fromProvider)
            );

            var comparer = new ValueComparer<string>(
                (l, r) => string.Equals(l, r, StringComparison.Ordinal),
                v => v.GetHashCode(),
                v => v
            );

            b.Property(x => x.Data)
                .HasConversion(converter, comparer)
                .HasColumnType("varbinary(max)")
                .IsRequired();

            b.Property(x => x.CreateTime)
                .HasDefaultValueSql("sysdatetime()")
                .ValueGeneratedOnAdd();

            b.Property(x => x.UpdateTime)
                .HasDefaultValueSql("sysdatetime()")
                .ValueGeneratedOnAddOrUpdate();

            b.Property(x => x.DataUpdateTime)
                .IsRequired();

            // —— 索引：SupId + MTHotelCode ——
            b.HasIndex(x => new { x.SupId, x.MTHotelCode })
                .HasDatabaseName("IX_SupHotelDataSource_Sup_MT");
        }
    }
}
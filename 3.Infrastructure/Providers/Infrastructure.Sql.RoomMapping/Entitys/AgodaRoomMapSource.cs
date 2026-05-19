using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys
{


    /// <summary>
    /// agoda匹配数据 xlsx 数据导入
    /// </summary>
    public class AgodaRoomMapSource : IHasEntityConfiguration<AgodaRoomMapSource>
    {
        public int? SupplierId { get; set; }
        public string? SupplierHotelId { get; set; }
        public string? SupplierHotelName { get; set; }
        public string? ChildRoomId { get; set; }
        public string? SupplierRoomId { get; set; }
        public string? SupplierRoomName { get; set; }
        public double? SupplierRoomSize { get; set; }
        public int? SupplierRoomOccupancy { get; set; }
        public int? AgodaHotel { get; set; }
        public string? AgodaHotelName { get; set; }
        public string? AgodaCountry { get; set; }
        public string? AgodaCity { get; set; }
        public string? AgodaAddress { get; set; }
        public int? AgodaRoomId { get; set; }
        public string? AgodaRoomName { get; set; }
        public double? AgodaRoomSize { get; set; }
        public int? AgodaRoomOccupancy { get; set; }

        public static void Configure(EntityTypeBuilder<AgodaRoomMapSource> entityBuilder)
        {
            entityBuilder.ToTable("AgodaRoomMapSource", "dbo");

            entityBuilder.HasNoKey(); // 没有定义主键，保持和数据库一致

            entityBuilder.Property(e => e.SupplierId);
            entityBuilder.Property(e => e.SupplierHotelId).HasMaxLength(255);
            entityBuilder.Property(e => e.SupplierHotelName).HasMaxLength(255);
            entityBuilder.Property(e => e.ChildRoomId).HasMaxLength(255);
            entityBuilder.Property(e => e.SupplierRoomId).HasMaxLength(255);
            entityBuilder.Property(e => e.SupplierRoomName).HasMaxLength(255);
            entityBuilder.Property(e => e.SupplierRoomSize);
            entityBuilder.Property(e => e.SupplierRoomOccupancy);

            entityBuilder.Property(e => e.AgodaHotel);
            entityBuilder.Property(e => e.AgodaHotelName).HasMaxLength(255);
            entityBuilder.Property(e => e.AgodaCountry).HasMaxLength(255);
            entityBuilder.Property(e => e.AgodaCity).HasMaxLength(255);
            entityBuilder.Property(e => e.AgodaAddress).HasMaxLength(255);
            entityBuilder.Property(e => e.AgodaRoomId);
            entityBuilder.Property(e => e.AgodaRoomName).HasMaxLength(255);
            entityBuilder.Property(e => e.AgodaRoomSize);
            entityBuilder.Property(e => e.AgodaRoomOccupancy);
        }
    }
}

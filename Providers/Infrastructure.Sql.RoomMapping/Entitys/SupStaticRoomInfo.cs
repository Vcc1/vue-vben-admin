using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    public sealed class SupStaticRoomInfo : IHasEntityConfiguration<SupStaticRoomInfo>
    {
        public long ID { get; set; }

        public string SHotelCode { get; set; } = string.Empty;

        public int SupId { get; set; }

        public string SRoomCode { get; set; } = string.Empty;

        public string RoomNameEN { get; set; } = string.Empty;

        public string BedInfo { get; set; } = string.Empty;

        public string ViewInfo { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; }

        public static void Configure(EntityTypeBuilder<SupStaticRoomInfo> builder)
        {
            builder.ToTable("SupStaticRoomInfo", "dbo");
            builder.HasKey(x => x.ID);

            builder.Property(x => x.ID).ValueGeneratedOnAdd();
            builder.Property(x => x.SHotelCode).IsRequired().HasMaxLength(64).UseCollation("Chinese_PRC_CI_AS");
            builder.Property(x => x.SupId).IsRequired();
            builder.Property(x => x.SRoomCode).IsRequired().HasMaxLength(128).UseCollation("Chinese_PRC_CI_AS");
            builder.Property(x => x.RoomNameEN).IsRequired().HasMaxLength(500).UseCollation("Chinese_PRC_CI_AS");
            builder.Property(x => x.BedInfo).IsRequired().HasMaxLength(1000).UseCollation("Chinese_PRC_CI_AS");
            builder.Property(x => x.ViewInfo).IsRequired().HasMaxLength(1000).UseCollation("Chinese_PRC_CI_AS");
            builder.Property(x => x.Area).IsRequired().HasMaxLength(100).UseCollation("Chinese_PRC_CI_AS");
            builder.Property(x => x.CreateTime).HasDefaultValueSql("sysdatetime()").ValueGeneratedOnAdd();

            builder.HasIndex(x => new { x.SupId, x.SHotelCode }).HasDatabaseName("IX_SupStaticRoomInfo_Sup_SHotel");
            builder.HasIndex(x => new { x.SupId, x.SHotelCode, x.RoomNameEN }).HasDatabaseName("IX_SupStaticRoomInfo_Sup_SHotel_ENName");
        }
    }
}

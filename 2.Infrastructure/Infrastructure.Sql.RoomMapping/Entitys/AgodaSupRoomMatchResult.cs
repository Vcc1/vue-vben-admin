using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    public class AgodaSupRoomMatchResult : IHasEntityConfiguration<AgodaSupRoomMatchResult>
    {
        public long Id { get; set; }
        public long SupRoomInfoId { get; set; }
        public string MTHotelCode { get; set; } = string.Empty;
        public string? OTARoomNameCN { get; set; }
        public string? OTARoomNameEN { get; set; }
        public DateTime CreateTime { get; set; }

        public static void Configure(EntityTypeBuilder<AgodaSupRoomMatchResult> builder)
        {
            builder.ToTable("AgodaSupRoomMatchResult");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.SupRoomInfoId).IsRequired();
            builder.Property(x => x.MTHotelCode).HasMaxLength(50).IsRequired();
            builder.Property(x => x.OTARoomNameCN).HasMaxLength(500);
            builder.Property(x => x.OTARoomNameEN).HasMaxLength(500);
            builder.Property(x => x.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();

            builder.HasIndex(x => new { x.MTHotelCode, x.SupRoomInfoId });
        }
    }
}

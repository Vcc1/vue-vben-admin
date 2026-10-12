using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.RoomMapping.Entitys
{

    /// <summary>
    /// agoda酒店房型 cvs数据导入
    /// </summary>
    public class AgodaHotelRoomSource : IHasEntityConfiguration<AgodaHotelRoomSource>
    {

        public int Id { get; set; }

        public string HotelRoomTypeName { get; set; } = string.Empty;

        public long HotelRoomTypeId { get; set; }

        public int SizeOfRoom { get; set; }

        public static void Configure(EntityTypeBuilder<AgodaHotelRoomSource> builder)
        {
            builder.ToTable("AgodaHotelRoomSource");
            builder.HasKey(x => new { x.Id, x.HotelRoomTypeId });
            builder.Property(x => x.Id)
                .IsRequired();
            builder.Property(x => x.HotelRoomTypeName)
                .HasMaxLength(255)
                .IsRequired();
            builder.Property(x => x.HotelRoomTypeId)
                .IsRequired();
            builder.Property(x => x.SizeOfRoom)
                .IsRequired();
        }
    }
}

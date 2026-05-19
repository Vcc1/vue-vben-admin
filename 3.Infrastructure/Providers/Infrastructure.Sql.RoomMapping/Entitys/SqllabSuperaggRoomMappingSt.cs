using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    public class SqllabSuperaggRoomMappingSt : IHasEntityConfiguration<SqllabSuperaggRoomMappingSt>
    {
        public int Id { get; set; }
        public int? DmcId { get; set; }
        public string? DmcName { get; set; }
        public string? DmcHotelId { get; set; }
        public string? DmcHotelName { get; set; }
        public int? ChildHotelId { get; set; }
        public int? MasterHotelId { get; set; }
        public int? HotelStatus { get; set; }
        public int? CountryId { get; set; }
        public string? CountryName { get; set; }
        public string? DmcRoomId { get; set; }
        public string? DmcRoomName { get; set; }
        public int? ChildRoomId { get; set; }
        public int? MasterRoomTypeId { get; set; }
        public string? MasterRoomCaptionText { get; set; }
        public string? MappingStatus { get; set; }
        public int? RoomStatus { get; set; }
        public double? SizeOfRoom { get; set; }
        public int? MaxOccupancy { get; set; }
        public string? BedTypeName { get; set; }
        public string? NumberOfBeds { get; set; }
        public string? OptionId { get; set; }
        public string? MappingStatus2 { get; set; }

        public static void Configure(EntityTypeBuilder<SqllabSuperaggRoomMappingSt> builder)
        {
            builder.ToTable("sqllab_superagg_room_mapping_st", "dbo");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("Id");
            builder.Property(x => x.DmcId).HasColumnName("dmc_id");
            builder.Property(x => x.DmcName).HasColumnName("dmc_name").HasMaxLength(255);
            builder.Property(x => x.DmcHotelId).HasColumnName("dmc_hotel_id").HasMaxLength(255);
            builder.Property(x => x.DmcHotelName).HasColumnName("dmc_hotel_name").HasMaxLength(255);
            builder.Property(x => x.ChildHotelId).HasColumnName("child_hotel_id");
            builder.Property(x => x.MasterHotelId).HasColumnName("master_hotel_id");
            builder.Property(x => x.HotelStatus).HasColumnName("hotel_status");
            builder.Property(x => x.CountryId).HasColumnName("country_id");
            builder.Property(x => x.CountryName).HasColumnName("country_name").HasMaxLength(255);
            builder.Property(x => x.DmcRoomId).HasColumnName("dmc_room_id").HasMaxLength(255);
            builder.Property(x => x.DmcRoomName).HasColumnName("dmc_room_name").HasMaxLength(255);
            builder.Property(x => x.ChildRoomId).HasColumnName("child_room_id");
            builder.Property(x => x.MasterRoomTypeId).HasColumnName("master_room_type_id");
            builder.Property(x => x.MasterRoomCaptionText).HasColumnName("master_room_caption_text").HasMaxLength(255);
            builder.Property(x => x.MappingStatus).HasColumnName("mapping_status").HasMaxLength(255);
            builder.Property(x => x.MappingStatus2).HasColumnName("MappingStatus2").HasMaxLength(255);
            builder.Property(x => x.RoomStatus).HasColumnName("room_status");
            builder.Property(x => x.SizeOfRoom).HasColumnName("size_of_room");
            builder.Property(x => x.MaxOccupancy).HasColumnName("max_occupancy");
            builder.Property(x => x.BedTypeName).HasColumnName("bed_type_name").HasMaxLength(255);
            builder.Property(x => x.NumberOfBeds).HasColumnName("number_of_beds").HasMaxLength(255);
            builder.Property(x => x.OptionId).HasColumnName("option_id").HasMaxLength(255);
        }
    }
}

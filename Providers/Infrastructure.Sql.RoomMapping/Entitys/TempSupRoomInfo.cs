using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("TempSupRoomInfo")]
    public class TempSupRoomInfo : IHasEntityConfiguration<TempSupRoomInfo>
    {
        public string SHotelCode { get; set; }
        public int SupId { get; set; }
        public string SRoomNameCN { get; set; }
        [Column(TypeName = "varchar(200)")]
        public string BedInfoKey { get; set; }
        public string SRoomCode { get; set; }
        public string MTHotelCode { get; set; }
        public string SRoomNameEN { get; set; }
        public string CreaterBy { get; set; }
        public DateTime? CreaterDate { get; set; }
        public string BedInfo { get; set; }
        public byte? Status { get; set; }
        public decimal? AcreageFrom { get; set; }
        public decimal? AcreageTo { get; set; }
        public int? SmokeType { get; set; }
        public int? MaxAdt { get; set; }
        public int? MaxChd { get; set; }

        public static void Configure(EntityTypeBuilder<TempSupRoomInfo> b)
        {
            b.HasNoKey();
        }
    }

    [Table("TempSupRoomInfo2")]
    public class TempSupRoomInfo2 : IHasEntityConfiguration<TempSupRoomInfo2>
    {
        public string SHotelCode { get; set; }
        public int SupId { get; set; }
        public string SRoomNameCN { get; set; }
        [Column(TypeName = "varchar(200)")]
        public string BedInfoKey { get; set; }
        public string SRoomCode { get; set; }
        public string MTHotelCode { get; set; }
        public string SRoomNameEN { get; set; }
        public string CreaterBy { get; set; }
        public DateTime? CreaterDate { get; set; }
        public string BedInfo { get; set; }
        public byte? Status { get; set; }
        public decimal? AcreageFrom { get; set; }
        public decimal? AcreageTo { get; set; }
        public int? SmokeType { get; set; }
        public int? MaxAdt { get; set; }
        public int? MaxChd { get; set; }

        public static void Configure(EntityTypeBuilder<TempSupRoomInfo2> b)
        {
            b.HasNoKey();
        }
    }

    [Table("TempSupRoomInfo2Log")]
    public class TempSupRoomInfo2Log : IHasEntityConfiguration<TempSupRoomInfo2Log>
    {
        public string SHotelCode { get; set; }
        public int SupId { get; set; }
        public string SRoomNameCN { get; set; }
        [Column(TypeName = "varchar(200)")]
        public string BedInfoKey { get; set; }
        public string SRoomCode { get; set; }
        public string MTHotelCode { get; set; }
        public string SRoomNameEN { get; set; }
        public string CreaterBy { get; set; }
        public DateTime? CreaterDate { get; set; }
        public string BedInfo { get; set; }
        public byte? Status { get; set; }
        public decimal? AcreageFrom { get; set; }
        public decimal? AcreageTo { get; set; }
        public int? SmokeType { get; set; }
        public int? MaxAdt { get; set; }
        public int? MaxChd { get; set; }
        public string Guid { get; set; }
        public static void Configure(EntityTypeBuilder<TempSupRoomInfo2Log> b)
        {
            b.HasNoKey();
        }
    }




}

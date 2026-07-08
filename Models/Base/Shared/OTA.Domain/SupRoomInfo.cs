using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Infrastructure.Sql.MengTuSup.Entitys
{



    [Table("SupRoomInfo")]
    public class SupRoomInfo
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string SHotelCode { get; set; }

        [Required]
        public int SupId { get; set; }

        [Required, StringLength(200)]
        [Column("SRoomName_CN")]
        public string SRoomNameCN { get; set; }

        [Required, StringLength(200)]
        public string BedInfoKey { get; set; }

        [Required, StringLength(100)]
        public string SRoomCode { get; set; }

        [StringLength(15)]
        public string MTHotelCode { get; set; }

        [StringLength(200)]
        [Column("SRoomName_EN")]
        public string SRoomNameEN { get; set; }

        //[StringLength(500)]
        //public string RoomDescription { get; set; }



        //[StringLength(50)]
        //public string LastUpdateBy { get; set; }
        [Column("LastUpdate_Date")]
        public DateTime? LastUpdateDate { get; set; }

        //public string BedType { get; set; } // varchar(max) 无长度限制

        public string BedInfo { get; set; } // varchar(max) 无长度限制

        public byte? Status { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? AcreageFrom { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? AcreageTo { get; set; }

        //[StringLength(200)]
        //public string AreaDescription { get; set; }

        public int? SmokeType { get; set; }

        public int? MaxAdt { get; set; }

        public int? MaxChd { get; set; }

        //[StringLength(50)]
        //public string Views { get; set; }

        //public int? MaxExtrabeds { get; set; }

        //[StringLength(50)]
        //public string IsTrusty { get; set; }
    }



    [Table("V_SupRoomInfo")]
    public class SupRoomInfoView
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string SHotelCode { get; set; }

        [Required]
        public int SupId { get; set; }

        [Required, StringLength(200)]
        [Column("SRoomName_CN")]
        public string SRoomNameCN { get; set; }

        [Required, StringLength(200)]
        public string BedInfoKey { get; set; }

        //[Required, StringLength(100)]
        //public string SRoomCode { get; set; }

        [StringLength(15)]
        public string MTHotelCode { get; set; }

        [StringLength(200)]
        [Column("SRoomName_EN")]
        public string SRoomNameEN { get; set; }

        //[StringLength(500)]
        //public string RoomDescription { get; set; }

        //[StringLength(50)]
        //public string CreaterBy { get; set; }

        //public DateTime? CreaterDate { get; set; }

        //[StringLength(50)]
        //public string LastUpdateBy { get; set; }

        //public DateTime? LastUpdateDate { get; set; }

        //public string BedType { get; set; } // varchar(max) 无长度限制

        //public string BedInfo { get; set; } // varchar(max) 无长度限制

        public byte? Status { get; set; }

        //[Column(TypeName = "decimal(6,2)")]
        //public decimal AcreageFrom { get; set; }

        //[Column(TypeName = "decimal(6,2)")]
        //public decimal AcreageTo { get; set; }

        //[StringLength(200)]
        //public string AreaDescription { get; set; }

        //public int SmokeType { get; set; }

        //public int MaxAdt { get; set; }

        //public int MaxChd { get; set; }

        //[StringLength(50)]
        //public string Views { get; set; }

        //public int MaxExtrabeds { get; set; }

        //[StringLength(50)]
        //public string IsTrusty { get; set; }

        public string CreateBy { get; set; }
        public int? OTARoomId { get; set; }
        public int? MappingStatus { get; set; }


    }

}

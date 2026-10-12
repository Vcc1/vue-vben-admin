using MTOTA.Entity.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("AgodaHotelPlatFormRoomInfo")]
    public class AgodaHotelPlatFormRoomInfo
    {
        [Key]
        public int ID { get; set; }
        public PlatForm PlatFormID { get; set; }
        public int HotelID { get; set; }
        public string MTHotelCode { get; set; }
        public int MTRoomCode { get; set; }
        public string PlatRoomCode { get; set; }
        public string RoomName { get; set; }
        public string RoomEnName { get; set; }
        public int MaxAdtNum { get; set; }
        public int? MaxChdNum { get; set; }

        public decimal AcreageFrom { get; set; }
        public decimal AcreageTo { get; set; }
        public int BedType { get; set; }
        public int WindowType { get; set; }
        public int NonSmoking { get; set; }
        public int OnLineStatus { get; set; }
        public DateTime CreateTime { get; set; }
        public int IsCreatedSellRoom { get; set; }
        public string CreateSellRoomFailMsg { get; set; }
        public DateTime? IsCreatedSellTime { get; set; }
        public int PushStatus { get; set; }
        public int AddPriceType { get; set; }
        public decimal AddPriceValue { get; set; }
        public string Creator { get; set; }

        public bool? IsMatch { get; set; }
        public bool? IsAutoCreated { get; set; }
        public int? MaxRoomNum { get; set; }
        public string FixPriceConfigStr { get; set; }

    }
}

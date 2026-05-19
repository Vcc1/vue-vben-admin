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
    [Table("AgodaHotelMateRoomInfoRelationship")]
    public class AgodaHotelMateRoomInfoRelationship
    {
        [Key]
        public int Id { get; set; }
        public int AgodaHotelRoomInfoId { get; set; }
        public int AgodaHotelMateRoomInfoId { get; set; }
        public bool IsDelete { get; set; }
        public string DeleteMsgType { get; set; }
        public string DeleteMsg { get; set; }
        public bool? EBKExist { get; set; }
    }
}

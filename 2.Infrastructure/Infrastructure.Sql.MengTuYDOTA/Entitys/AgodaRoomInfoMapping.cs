using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("AgodaRoomInfoMapping")]
    public class AgodaRoomInfoMapping
    {
        public string MTHotelCode { get; set; }
        public string RoomEnName { get; set; }
        [Column("supplier_room_name")]
        public string SupplierRoomName { get; set; }
        [Column("agoda_master_room_name")]
        public string AgodaMasterRoomName { get; set; }
    }
}

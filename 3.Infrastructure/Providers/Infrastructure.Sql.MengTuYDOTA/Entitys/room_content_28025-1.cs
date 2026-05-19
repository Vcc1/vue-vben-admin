using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    public class room_content_28025_1
    {
        [Column("ID")]
        [Key]
        public int ID { get; set; }
        [Column("supplier_room_id")]
        public string SupplierRoomId { get; set; }
        [Column("supplier_hotel_id")]
        public string SupplierHotelId { get; set; }
        [Column("supplier_room_name")]
        public string SupplierRoomName { get; set; }

    }
}

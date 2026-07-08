using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys.Views
{
    [Table("156匹配关系数据导入")]
    public class 匹配关系数据导入156
    {
        public string MTHotelCode { get; set; }
        public int MTRoomCode { get; set; }
        public string name { get; set; }
        public int? supplier_room_id { get; set; }
        public string supplier_hotel_id { get; set; }
    }

    [Table("166匹配关系数据导入")]
    public class 匹配关系数据导入166
    {
        public string MTHotelCode { get; set; }
        public int MTRoomCode { get; set; }
        public string name { get; set; }
        public int? supplier_room_id { get; set; }
        public int? supplier_hotel_id { get; set; }
    }
}

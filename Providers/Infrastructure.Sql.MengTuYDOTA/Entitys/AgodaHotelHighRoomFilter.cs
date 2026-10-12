using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("AgodaHotelHighRoomFilter")]
    public class AgodaHotelHighRoomFilter
    {
        [Key]
        public int Id { get; set; }
        public string RoomEnName { get; set; }
    }
}

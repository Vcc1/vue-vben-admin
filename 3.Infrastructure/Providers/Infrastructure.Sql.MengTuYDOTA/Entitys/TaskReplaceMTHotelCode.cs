using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("TaskReplaceMTHotelCode")]
    public class TaskReplaceMTHotelCode
    {
        [Key]
        public int Id { get; set; }
        public int PlatForm { get; set; }
        public string OldMTHotelCode { get; set; }
        public string NewMTHotelCode { get; set; }
        public string Data { get; set; }
        public DateTime CreateTime { get; set; }
    }
}

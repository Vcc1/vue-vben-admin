using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("SupRoomInfoStat")]
    public class SupRoomInfoStat
    {
        [Key]
        public int Id { get; set; }

        public int SupId { get; set; }

        public int TotalCount { get; set; }

        public int WithMTHotelCodeCount { get; set; }

        public int WithRoomMapCount { get; set; }

        [Column(TypeName = "date")]
        public DateTime StatDate { get; set; }   // date 类型 → C# 里用 DateTime（只保留日期部分）

        public DateTime InsertTime { get; set; }

        public Guid BatchId { get; set; }

        public int? UnmatchedRecentCount { get; set; }
    }
}

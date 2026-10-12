using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    // 供应商酒店信息视图（位于 RoomMapping 数据库）
    // 仅用于读取 Remark 等信息
    [Keyless]
    [Table("SupHotelInfoView")] // 如实际视图名不同，请按需调整
    public class SupHotelInfoView
    {
        [StringLength(100)]
        public string SHotelCode { get; set; }

        public int SupId { get; set; }

        [StringLength(15)]
        public string MTHotelCode { get; set; }

        public string Remark { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MixedData.Entitys
{
    [Table("QueryPriceContrast")]
    public class QueryPriceContrast
    {
        [Key]
        public long Id { get; set; } // 主键ID

        [Required]
        [StringLength(50)]
        public string BatchNumber { get; set; } // 批次号

        [Required]
        public int ContrastId { get; set; } // 对比ID

        [Required]
        public int SupId { get; set; } // 供应商ID

        [Required]
        [StringLength(50)]
        public string MTHotelCode { get; set; } // MTHotelCode

        [Required]
        [StringLength(255)]
        public string SupplierName { get; set; } // 供应商名称

        [Required]
        [StringLength(255)]
        public string SupRoomName { get; set; } // 房间名称

        [StringLength(255)]
        public string SupRoomEnName { get; set; } // 房间英文名称

        [Required]
        [StringLength(50)]
        public string SupRoomCode { get; set; } // 房间代码

        [Required]
        [StringLength(50)]
        public string SupHotelCode { get; set; } // 供应商酒店代码

        [StringLength(500)]
        public string SupBedInfo { get; set; } // 床型信息

        [StringLength(50)]
        public string BedInfoKey { get; set; } // 床型信息Key

        [Required]
        public int Person { get; set; } // 入住人数

        [Required]
        public bool IsBreakfast { get; set; } // 是否包含早餐

        [Required]
        [DataType(DataType.Date)]
        public DateTime CheckIn { get; set; } // 入住时间

        [Required]
        [DataType(DataType.Date)]
        public DateTime CheckOut { get; set; } // 退房时间

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SupTotalPrice { get; set; } // 供应商总价

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SupTotalPriceRMB { get; set; } // 供应商总价人民币

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 创建时间

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // 更新时间
    }
}

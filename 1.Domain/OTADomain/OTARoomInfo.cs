using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("OTARoomInfo")]
    public class OTARoomInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string MTHotelCode { get; set; }

        [Required]
        [MaxLength(150)]
        public string RoomNameCN { get; set; }

        [MaxLength(200)]
        public string? RoomNameEN { get; set; }

        [Required]
        public byte Person { get; set; }

        public int? RoomQuantity { get; set; }

        public short? FloorMin { get; set; }

        public short? FloorMax { get; set; }

        public short? AreaMin { get; set; }

        public short? AreaMax { get; set; }

        public byte? Smoke { get; set; }

        [Required]
        public DateTime CreateTime { get; set; }

        public int? Status { get; set; }

        [MaxLength(50)]
        public string? Author { get; set; }

        public int? Source { get; set; }

        //1:老数据导入  2:EBK添加  3:代理商房型添加 4匹配添加
        public int SourceType { get; set; }

        [MaxLength(200)]
        public string DefaultWord { get; set; }
        [MaxLength(500)]
        public string BedInfo { get; set; }

        public OTARoomInfoExtension? Extension { get; set; }
    }

}

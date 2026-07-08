using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    /// <summary>
    /// EBK写母房型预匹配表
    /// </summary>
    [Table("EBKRoomMapping")]
    public class EBKRoomMapping
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 母房型名称
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string RoomName { get; set; }

        /// <summary>
        /// 美团酒店编码
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string MtHotelCode { get; set; }

        /// <summary>
        /// 匹配后的房型名
        /// </summary>
        [MaxLength(255)]
        public string? MRoomName { get; set; }

        /// <summary>
        /// 批次号
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string BatchNo { get; set; }
    }
}

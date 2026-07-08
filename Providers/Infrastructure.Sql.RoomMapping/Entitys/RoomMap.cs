using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    [Table("RoomMap")]
    public class RoomMap
    {
        [Key]
        public int Id { get; set; }

        public string MTHotelCode { get; set; }
        [Required]
        public int SupRoomId { get; set; }
        [Required]
        public int OTARoomId { get; set; }
        [Required]
        public int Status { get; set; }
        [MaxLength(50)]
        public string? CreateBy { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public int? MatchType { get; set; }
        [Column(TypeName = "decimal(5,4)")]
        public decimal? MatchScore { get; set; }
        /// <summary>
        /// 0 未知；1 符合；2 不符合
        /// </summary>
        public int? BedStatus { get; set; }
    }
}

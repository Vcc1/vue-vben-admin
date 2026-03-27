using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructures.Sql.MengTuAPI.Entitys
{
    [Table("RecordHotelIsPrice")]
    public class RecordHotelIsPrice
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        public int Id { get; set; }
        public string MTHotelCode { get; set; }
        public string SHotelCode { get; set; }
        public string SupFlag { get; set; }
        public char? IsPrice { get; set; }
        public DateTime? CreateDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public decimal? RMBPrice { get; set; }

    }
}
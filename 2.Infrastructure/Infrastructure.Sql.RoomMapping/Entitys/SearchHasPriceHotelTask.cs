using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Sql.MengTuSup.Entitys
{
    [Table("SearchHasPriceHotelTask")]
    public class SearchHasPriceHotelTask
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string? MTHotelCode { get; set; }

        [StringLength(100)]
        public string? SHotelCode { get; set; }

        [StringLength(30)]
        public string? SContryCode { get; set; }

        [StringLength(100)]
        public string? ChainName { get; set; }

        public bool? HasPrice { get; set; }

        public DateTime CreateTime { get; set; } = DateTime.Now;

        public int State { get; set; } = 0;

        public int SupId { get; set; }

        [StringLength(100)]
        public string? SCityCode { get; set; }

        public string? HasPriceTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinPrice { get; set; }

        [StringLength(100)]
        public string? SupFlag { get; set; }
    }
}
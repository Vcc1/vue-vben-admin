using MTOTA.Entity.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("StatisticsHotelPrice")]
    public class StatisticsHotelPrice
    {
        [Key]
        public int Id { get; set; }
        public string BatchNo { get; set; }
        public string MTHotelCode { get; set; }
        public PlatForm PlatForm { get; set; }
        public bool IsEBK { get; set; }
        public bool IsPlatformPriceApi { get; set; }
        public bool IsCachePrice { get; set; }
        [Column(TypeName = "DateTime")]
        public DateTime CreateTime { get; set; }
    }
}

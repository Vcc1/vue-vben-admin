using MTOTA.Entity.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    /// <summary>
    /// 平台酒店售卖规则
    /// </summary>
    [Table("PlatformHotelSaleRuleLog")]
    public class PlatformHotelSaleRuleLog
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Column("ID")]
        [Key]
        public int ID { get; set; }
        public PlatForm PlatFormID { get; set; }
        public string MTHotelCode { get; set; }
        public SaleRuleType SaleRuleType { get; set; }
        public string SaleRuleValue { get; set; }

        /// <summary>
        /// 启用状态
        /// </summary>
        public bool Status { get; set; }
        public DateTime CreateTime { get; set; }
        public string CreateBy { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public string LastUpdateBy { get; set; }

    }

}

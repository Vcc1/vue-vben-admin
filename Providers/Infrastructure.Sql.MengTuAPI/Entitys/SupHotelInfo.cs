using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructures.Sql.MengTuAPI.Entitys
{
    [Table("SupHotelInfo")]
    public class SupHotelInfo
    {
        [Key]
        public int Id { get; set; }
        public string SHotelCode { get; set; }
        public int SupId { get; set; }
        public string SupFlag { get; set; }
        public string MTHotelCode { get; set; }
        public int State { get; set; }
        /// <summary>
        /// 供应商酒店英文(JiaXian，NTA2个供应商需要，优先英文名称)
        /// </summary>
        [Column("SHotelName_En")]
        public string SHotelNameEn { set; get; }

        [Column("SHotelName_CHS")]
        public string SHotelNameCHS { set; get; }

        /// 供应商国家Code
        /// </summary>
        public string SContryCode { set; get; }
        public string SAddress { set; get; }
        /// <summary>
        /// 供应商城市code
        /// </summary>
        public string SCityCode { set; get; }
        /// <summary>
        /// 供应商备注 （TeamAmerica再此字段存储区分 B2B与B2C的帐号)
        /// </summary>
        public string Remark { set; get; }
        public string SCityName { get; set; }
        public string SContryName { get; set; }
        public decimal? SLatitude { get; set; }
        public decimal? SLongitude { get; set; }
        public string SHotelTel { get; set; }
        public string SStarLevel { get; set; }
        public string PostCode { get; set; }
    }
}
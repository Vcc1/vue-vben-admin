using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MixedData.Entitys
{
    [Table("SupHotelDataSource")]
    public class SupHotelDataSource
    {
        /// <summary>
        /// 主键
        /// </summary>
        public long ID { get; set; }

        /// <summary>
        /// 美团酒店编码
        /// </summary>
        public string MTHotelCode { get; set; }

        /// <summary>
        /// 供应商酒店编码
        /// </summary>
        public string SHotelCode { get; set; }

        /// <summary>
        /// 供应商ID
        /// </summary>
        public int SupId { get; set; }

        /// <summary>
        /// 原始数据（JSON）
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// 数据更新时间（业务字段）
        /// </summary>
        public DateTime DataUpdateTime { get; set; }
    }

}
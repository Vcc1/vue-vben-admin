using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{

    [Table("AutoBookingOrder")]
    public class AutoBookingOrder
    {
        [Key]
        public string Id { get; set; }

        public int PlatFormId { get; set; }

        /// <summary>
        /// OTA平台订单号
        /// </summary>
        public string OTAOrderNo { get; set; }
        /// <summary>
        /// 对接平台订单号
        /// </summary>
        public string PlatFormOrderNo { get; set; }

        public DateTime CreateTime { get; set; } = DateTime.Now;
        public string SupOrderNo { get; set; }

        /// <summary>
        /// 0待定
        /// 1订单成功
        /// 2订单失败
        /// </summary>
        public int OrderStatus { get; set; }

        /// <summary>
        /// 0未通知
        /// 1已通知
        /// 2通知失败
        /// </summary>
        public int NoticeStatus { get; set; }
        public string Message { get; set; }

    }
}

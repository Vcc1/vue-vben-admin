using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("OTAHotelInfo")]
    public class OTAHotelInfo
    {
        [Key]
        public int ID { get; set; }
        public string MTHotelCode { get; set; }
    }
    /// <summary>
    /// 预定订单表
    /// </summary>
    /// 
    [Table("YD_Order")]
    public class YDOrder
    {
        /// <summary>
        /// 编号
        /// </summary>
        /// 
        [Column("Id")]
        [Key]
        public int Id { get; set; }
        public string MTOrderCode { get; set; }
        public string MTHotelCode { get; set; }
        public string HotelName { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int RoomNum { get; set; }
        public string RoomName { get; set; }
        public decimal Price { get; set; }
        public decimal? SupPrice { get; set; }
        public int? Currency { get; set; }
        public DateTime? LastCancelDate { get; set; }
        public string RoomInfo { get; set; }
        public int? Status { get; set; }
        public int? SupStatus { get; set; }
        public int? CancelStatus { get; set; }
        public string CreateBy { get; set; }
        public DateTime CreateDate { get; set; }
        public string PlatOrderNo { get; set; }
        public string UserAgent { get; set; }
        public string IpAddress { get; set; }
        public int? SyncCount { get; set; }
        public string SupOrderCode { get; set; }
        public string Meals { get; set; }
        public string Remark { get; set; }
        public string SupFlag { get; set; }
        public bool? IsSyncB2B { get; set; }
        public int? PlatId { get; set; }
        public int? NationId { get; set; }
        public string UserRemark { get; set; }
        public bool? IsAutoBook { get; set; }
        public string Source { get; set; }
        public int? ReStatus { get; set; }
        public bool? IsMobile { get; set; }
        public bool? IsReturn { get; set; }
        public bool IsFollow { get; set; }
        public bool IsNote { get; set; }
        public int? HoldId { get; set; }
        public DateTime LastSyncTime { get; set; }
        public string Description { get; set; }
        public string RoomInfoJson { get; set; }
        public string MTRoomCode { get; set; }
        public string RealOrderNo { get; set; }
        public decimal? SupCostPrice { get; set; }
        public bool? IsHoldOrder { get; set; }
        public int? ConfirmStatus { get; set; }

    }
}

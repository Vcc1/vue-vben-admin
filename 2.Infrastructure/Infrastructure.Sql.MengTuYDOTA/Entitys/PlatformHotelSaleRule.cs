using MTOTA.Entity.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    /// <summary>
    /// 平台酒店售卖规则
    /// </summary>
    [Table("PlatformHotelSaleRule")]
    public class PlatformHotelSaleRule
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



    /// <summary>
    /// 售卖规则类型
    /// </summary>
    public enum SaleRuleType
    {
        /// <summary>
        /// 取消规则
        /// </summary>
        CancelRule = 1,
        /// <summary>
        /// 近日期无价调价
        /// </summary>
        RecentDaysNoPrice = 2,
        /// <summary>
        /// 无价不售卖
        /// </summary>
        NoPriceNotSale = 200,
        /// <summary>
        /// 无价调价
        /// </summary>
        NoPriceAddPrice = 201,
        /// <summary>
        /// 根据入住天数无价不售卖
        /// </summary>
        NoPriceNotSaleByCheckIn = 202,
        /// 手工价只售卖保留房  Ctrip专用
        /// </summary>
        FixPriceSaleInventoryRoom = 3,

        /// <summary>
        /// 过滤供应商Id List
        /// </summary>
        FilterSupList = 4,
        /// <summary>
        /// 最小入住天数
        /// </summary>
        CheckInMinDay = 5,
        /// <summary>
        /// 启用供应商直连查询
        /// </summary>
        RealTimePriceBySup = 6,
        /// <summary>
        /// 最大间数
        /// </summary>
        MaxRoomNumRule = 7,
        /// <summary>
        /// 自动 下单
        /// </summary>
        AutoBooking = 8,
        /// <summary>
        /// 按国籍价格输出
        /// </summary>
        NationalityRule = 9,
        /// <summary>
        /// 强制售完自动关房  Ctrip专用
        /// </summary>
        AutoCloseRoom = 10,
        /// <summary>
        /// 自定义供应商售卖规则
        /// </summary>
        CustomSupSellRule = 11,
        /// <summary>
        /// 来单自动向供应商下单
        /// </summary>
        SupAutoBooking = 12,
        /// <summary>
        /// 无早卖有早
        /// </summary>
        NHBToHHB = 13,
        /// <summary>
        /// 低价调价
        /// </summary>
        LowPriceRule = 14,
        /// <summary>
        /// 自定义价格来源优先规则
        /// </summary>
        CustomPriceSourceFirstRule = 15,
        /// <summary>
        /// 最大间数
        /// </summary>
        DateMaxRoomNumRule = 16,

    }

    #region 取消规则 

    public class HotelSale_CancelRule
    {
        /// <summary>
        /// 近天数
        /// </summary>
        public int Days { get; set; }

        /// <summary>
        /// %
        /// </summary>
        public decimal AddPriceVal { get; set; }
    }

    #endregion


    public class HotelRuleInfoModel
    {
        public PlatForm PlatFormID { get; set; }
        public string MTHotelCode { get; set; }

        //取消政策
        public string CancelRuleType { get; set; }
        public string CancelTimeLimit { get; set; }
        public bool CancelRuleStatus { get; set; }

        //近天断房
        public string RecentDaysNoPriceType { get; set; }
        public int RecentDaysNoPriceDay { get; set; }

        /// <summary>
        /// 过滤供应商Id
        /// </summary>
        public List<int> FilterSupList { get; set; }
        public List<string> FilterSupFlagList { get; set; }

        /// <summary>
        /// 最小入住天数
        /// </summary>
        //public int CheckInMinDay { get; set; }
        public string EnabledCheckInMinDayRule { get; set; }
        public List<int> CheckInMinDayRule { get; set; }

        /// <summary>
        /// 最大间数
        /// </summary>
        public string MaxRoomNumRuleType { get; set; }
        public string MaxRoomNumRule { get; set; }
        public string DateMaxRoomNumRuleType { get; set; }
        public string DateMaxRoomNumRule { get; set; }


        /// <summary>
        /// 查价方式
        /// </summary>
        public string EnabledSupPrice { get; set; }

        //自动下单
        public string AutoBookingRule { get; set; }

        /// <summary>
        /// 按国籍价格输出
        /// </summary>
        public string NationalityRule { get; set; }

        public string EnabledCustomSupSellRule { get; set; }
        public string CustomSupSellRule { get; set; }

        //供应商自动下单规则
        public string SupAutoBooking { get; set; }

        /// <summary>
        /// 无早卖有早
        /// </summary>
        public string NHBToHHB { get; set; }

        /// <summary>
        /// 低价调价
        /// </summary>
        public string LowPriceRule { get; set; }

        #region 价格优先规则

        public string EnabledCustomPriceSourceFirstRule { get; set; }
        public string CustomPriceSourceFirstRule { get; set; }

        #endregion

    }


    public class Canc1elRule
    {
        public int Days { get; set; }

        public decimal AddPriceVal { get; set; }
    }

    public class CtripHotelSellRuleModel
    {
        public PlatForm PlatFormID { get; set; }
        public int SubHotelId { get; set; }



        /// <summary>
        /// 取消税费
        /// 0 跟随配置,1 不可取消, 2 限时取消, 3 免费取消,4 特殊取消
        /// </summary>
        public string CancelRuleType { get; set; }
        /// <summary>
        /// 最晚取消天数  
        /// 0-90
        /// </summary>
        public int LimitLastCancleDay { get; set; }
        /// <summary>
        /// 最晚取消小时数
        /// 0-23
        /// </summary>
        public int LimitLastCancleHour { get; set; }
        /// <summary>
        /// 限时取消扣费方式  
        /// 1.扣首晚,2.扣全款
        /// </summary>
        public int LimitLastCancleTax { get; set; }
        public bool CancleRuleStatus { get; set; }
        /// <summary>
        /// 手工价只卖保留房
        /// </summary>
        public bool FixPriceSaleInventoryRoom { get; set; }

        public bool AutoCloseRoom { get; set; }
    }

    public class AutoBookingRule
    {
        public int MTNationId { get; set; }
        public string MTNationIdName { get; set; }
        public string CreateBy { get; set; }
        public string CreateByName { get; set; }
        public string Phone { get; set; }
        public int HightPrice { get; set; }
        public string NotSupIdList { get; set; }
        public string SupIdList { get; set; }
        public bool SendRemark { get; set; }
        public AdjustPriceType AddPriceType { get; set; } = AdjustPriceType.比例值上调;
        public decimal AddPriceValue { get; set; }
    }

    public class NHBToHHBRule
    {
        public bool IsEnabled { get; set; }
        public AdjustPriceType AddPriceType { get; set; } = AdjustPriceType.比例值上调;
        public decimal AddPriceValue { get; set; }
    }

    public class LowPriceRule
    {
        public bool IsEnabled { get; set; }
        public AdjustPriceType AddPriceType { get; set; } = AdjustPriceType.比例值上调;
        public decimal AddPriceValue { get; set; }
        public int LowPriceLte { get; set; }
    }


    public class PricePriorityStrategy
    {
        /// <summary>
        /// 顺序
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// 供应商ID
        /// </summary>
        public int SupId { get; set; }
        public string SupFlag { get; set; }
        /// <summary>
        /// 加点类型
        /// </summary>
        public AdjustPriceType APT { get; set; }
        /// <summary>
        /// 加点值
        /// </summary>
        public decimal APV { get; set; }
        public bool Enabled { get; set; }
    }
}

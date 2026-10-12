/******************************************************
 * 项目名称: MTOTA.Entity.Bussiness
 * 项目描述:
 * 类 名 称: AgodaHotelInfo
 * 版 本 号:
 * 说    明:
 * 作    者：FlyFish
 * 创建时间：2019/8/26 17:45:57
 *******************************************************
 * Copyright @ FlyFish 2019. All right reserved.
******************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using MTOTA.Entity.Enum;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    /// <summary>
    /// 携程海外海外酒店信息信息
    /// </summary>
    [Table("NewCtripHotelInfo")]
    public class NewCtripHotelInfo
    {

        /// <summary>
        /// 主键ID
        /// </summary>
        [Column("ID")]
        [Key]
        public int ID { get; set; }
        /// <summary>
        /// 平台ID
        /// </summary>
        public PlatForm PlatFormID { get; set; }

        /// <summary>
        /// 萌兔酒店CODE
        /// </summary>
        [Column("MTHotelCode")]
        [StringLength(50)]
        [Required]
        public string MTHotelCode { get; set; } = "";

        /// <summary>
        /// 平台的酒店Code
        /// </summary>
        [Column("PlatHotelCode")]
        [StringLength(50)]
        [Required]
        public string PlatHotelCode { get; set; } = "";

        /// <summary>
        /// 酒店中文名称
        /// </summary>
        [StringLength(100)]
        public string HotelName { get; set; }

        /// <summary>
        /// 酒店英文名称
        /// </summary>
        [StringLength(100)]
        public string HotelEnName { get; set; }


        /// <summary>
        /// 酒店中文地址
        /// </summary>
        [StringLength(500)]
        [Required]
        public string HotelAddress { get; set; }

        /// <summary>
        /// 酒店英文地址
        /// </summary>
        [StringLength(500)]
        [Required]
        public string HotelEnAddress { get; set; }

        /// <summary>
        /// 经度
        /// </summary>
        [StringLength(20)]
        public string Longitude { get; set; }

        /// <summary>
        /// 纬度
        /// </summary>
        [StringLength(20)]
        public string Latitude { get; set; }


        /// <summary>
        ///国家ISO 编码
        /// </summary>
        [StringLength(5)]
        public string CountryCode { get; set; }

        /// <summary>
        ///国家名称
        /// </summary>
        [StringLength(100)]
        public string CountryName { get; set; }

        /// <summary>
        ///国家名称
        /// </summary>
        [StringLength(100)]
        public string CountryEnName { get; set; }

        /// <summary>
        /// 城市名
        /// </summary>
        [StringLength(100)]
        public string CityName { get; set; }

        /// <summary>
        /// 城市英文名
        /// </summary>
        [StringLength(100)]
        public string CityEnName { get; set; }

        /// <summary>
        /// 酒店联系电话
        /// </summary>
        [StringLength(50)]
        public string HotelTel { get; set; }

        /// <summary>
        /// 平台上线状态
        /// </summary>
        public PlatformStatus OnLineStatus { get; set; } = PlatformStatus.新建;

        #region 调价规则
        /// <summary>
        /// 1固定值上调 2 比例值上调 3 固定值下调 4 比例值下调
        /// </summary>
        public AdjustPriceType AddPriceType { get; set; } = AdjustPriceType.比例值上调;

        /// <summary>
        /// 调价值
        /// </summary>
        public decimal AddPriceValue { get; set; } = 5;
        #endregion
        /// <summary>
        /// 是否拉取了房型
        /// </summary>
        public int IsPullRoom { get; set; } = 0;
        /// <summary>
        /// 对应mappedMaster  是否匹配母酒店字段  0未匹配  1 匹配成功 2暂不匹配
        /// </summary>
        public int MatchStatus { get; set; } = 0;
        /// <summary>
        /// 携程子酒店接口直连的状态，1.可订，2.不可订，3.废弃，等； 对应interfaceStatus字段  
        /// </summary>
        public int PlatOnLineStatus { set; get; } = -1;

        public DateTime CreateTime { get; set; }
        /// <summary>
        /// 推送状态 0未推送  1推送成功 -1推送失败
        /// </summary>
        public int PushStatus { get; set; } = 0;

        public string CtripHotelId { set; get; }

        public string MappingMsg { set; get; }

        public string CtripTelePhone { get; set; }

        public string CtripBranName { get; set; }
        /// <summary>
        /// 批次号
        /// </summary>
        public string BatchNo { get; set; }
        /// <summary>
        /// 携程子酒店的状态，-1 废弃，1-可订，0-不可订等；  对应Hotelstatus字段
        /// </summary>
        public int CtripOnlineStatus { get; set; }
    }
}

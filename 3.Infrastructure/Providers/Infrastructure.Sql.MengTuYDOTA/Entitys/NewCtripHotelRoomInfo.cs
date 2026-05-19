using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MTOTA.Entity.Enum;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    /// <summary>
    /// 携程海外海外 酒店房型
    /// </summary>
    [Table("NewCtripHotelRoomInfo")]
    public class NewCtripHotelRoomInfo
    {

        /// <summary>
        /// 主键ID
        /// </summary>
        [Column("ID")]
        [Key]
        public int ID { get; set; }

        /// <summary>
        /// 酒店ID
        /// </summary>
        public int HotelID { get; set; }
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
        /// 是否上线
        /// </summary>
        [Required]
        public PlatformStatus OnLineStatus { get; set; } = PlatformStatus.新建;

        /// <summary>
        /// 萌兔房型编码
        /// </summary>

        public string MTRoomCode { get; set; }

        /// <summary>
        /// 平台房型编码
        /// </summary>
        public string PlatRoomCode { get; set; } = "";

        /// <summary>
        /// 携程房型编码
        /// </summary>
        public string CtripRoomCode { get; set; }

        /// <summary>
        /// 萌兔房型名称
        /// </summary>
        [StringLength(100)]
        public string RoomName { get; set; }

        /// <summary>
        /// 萌兔房型英文名称
        /// </summary>
        [StringLength(120)]
        public string RoomEnName { get; set; }

        /// <summary>
        /// 入住最大成人数
        /// </summary>
        public int? MaxAdtNum { get; set; }

        /// <summary>
        /// 入住最大儿童数
        /// </summary>
        public int? MaxChdNum { get; set; }

        /// <summary>
        /// 最小面积  m²
        /// </summary>
        public decimal AcreageFrom { get; set; }

        /// <summary>
        /// 最大面积  m²
        /// </summary>
        public decimal AcreageTo { get; set; }

        /// <summary>
        /// 床型  4  未知   1大床    2 双床 3 大床或双床   5   单人床   6小型双人床
        /// </summary>      
        public int BedType { get; set; }

        /// <summary>
        /// 是否有窗   0无窗 1有窗 2部分有窗   3 未知   4 内窗  5 天窗   6 封闭窗 7 飘窗 
        /// </summary>

        public int WindowType { get; set; }

        /// <summary>
        /// 是否禁烟  -1未知 0允许 1禁止  2 部分可吸烟
        /// </summary>      
        public int NonSmoking { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>

        public DateTime CreateTime { get; set; }


        /// <summary>
        /// 携程基础房型的状态，-1.删除，1.有效，0.隐藏等；	
        /// </summary>
        public int CtripOnLineStatus { set; get; } = 0;

        /// <summary>
        /// -1 未知  0已推未知 1 已推上线  2 已推下线 3 已推失败
        /// </summary>
        public int PullStatus { get; set; } = -1;

        public int MatchStatus { get; set; } = -1;

        public AdjustPriceType AddPriceType { set; get; }

        public decimal AddPriceValue { get; set; }
        /// <summary>
        /// -1 未知  0 创建失败 1 创建成功  2 创建中
        /// </summary>
        public int CreateSellPricePlan { set; get; }

        public string FailMsg { get; set; }

        /// <summary>
        /// 携程基础房型接口直连的状态，1.正常，0.废弃等；（重开也算正常）	
        /// </summary>
        public int PlatOnLineStatus { get; set; } = 0;

    }

}


using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
//using MTOTA.Entity.Enum;

namespace Infrastructures.Sql.MengTuAPI.Entitys
{

    /// <summary>
    /// 房型信息
    /// </summary>
    [Table("AgodaHotelRoomInfoVerify")]
    public class AgodaHotelRoomInfoVerify
    {

        /// <summary>
        /// 主键ID
        /// </summary>
        [Column("ID")]
        [Key]
        public int ID { get; set; }

        /// <summary>
        /// 萌兔酒店CODE
        /// </summary>
        [Column("MTHotelCode")]
        [StringLength(50)]
        [Required]
        public string MTHotelCode { get; set; }

        /// <summary>
        /// 平台上线状态
        /// </summary>
        //public PlatformStatus OnLineStatus { get; set; } = PlatformStatus.新建;


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
        public int MaxAdtNum { get; set; }

        /// <summary>
        /// 入住最大儿童数
        /// </summary>
        public int MaxChdNum { get; set; }

        /// <summary>
        /// 最小面积  m²
        /// </summary>
        public decimal AcreageFrom { get; set; }

        /// <summary>
        /// 最大面积  m²
        /// </summary>
        public decimal AcreageTo { get; set; }
        /// <summary>
        /// 床型
        /// </summary>      
        public int BedType { get; set; }

        /// <summary>
        /// 是否有窗
        /// </summary>

        public int WindowType { get; set; }

        /// <summary>
        /// 是否禁烟
        /// </summary>      
        public int NonSmoking { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>

        public DateTime CreateTime { get; set; }
        public DateTime? RoomVerifyTime { get; set; }
        public DateTime? PushTime { get; set; }

        /// <summary>
        /// 推送状态
        /// </summary>
        public int PushStatus { get; set; }


        public string Creator { get; set; } = "";

        /// <summary>
        /// 房间数
        /// </summary>
        public int MaxRoomNum { get; set; }

        public int RoomVerifyStatus { get; set; }
        public string RoomVerifyRemark { get; set; }
        public string PushStatusMsg { get; set; }

        public int? MTRoomCode { get; set; }
        public int? UpdateType { get; set; }
        public string BedTypeDesc { get; set; }
        public int? RoomViewCode { get; set; }
        public int? NumberOfBedroom { get; set; }
        public int? NumberOfBathroom { get; set; }
        public string AmenityCode { get; set; }

    }
}

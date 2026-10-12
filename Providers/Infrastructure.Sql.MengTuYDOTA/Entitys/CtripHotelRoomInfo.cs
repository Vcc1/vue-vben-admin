using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.MengTuYDOTA.Entitys
{
    [Table("CtripHotelRoomInfo")]
    public class CtripHotelRoomInfo
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Column("ID")]
        [Key]
        public int ID { get; set; }

        /// <summary>
        /// 母酒店ID
        /// </summary>
        [Column("masterHotelID")]
        public int masterHotelID { get; set; }

        /// <summary>
        /// 母物理房型ID 
        /// </summary>
        [Column("masterBasicRoomId")]
        public int masterBasicRoomId { get; set; }

        /// <summary>
        /// 物理房型名称
        /// </summary>
        [Column("basicRoomName")]
        [StringLength(100)]
        [Required]
        public string basicRoomName { get; set; }
        /// <summary>
        /// 萌兔酒店编码
        /// </summary>
        public string MTHotelCode { get; set; }

        /// <summary>
        /// 物理房型英文名称
        /// </summary>
        [Column("basicRoomNameEn")]
        [StringLength(200)]
        public string basicRoomNameEn { get; set; } = "";

        /// <summary>
        /// 最大入住人数
        /// </summary>
        public int person { get; set; } = 2;

        /// <summary>
        /// 海外酒店-房型数量 (非必传)
        /// </summary>
        [Column("roomQuantity")]
        [StringLength(15)]
        public int? roomQuantity { get; set; } = 0;

        /// <summary>
        /// 楼层
        /// </summary>
        [Column("floorRange")]
        [StringLength(50)]
        public string floorRange { get; set; } = "";

        /// <summary>
        /// 海外酒店-窗 (非必传)
        /// 0-无窗，1-部分有窗，2-有窗，3-未知，4-内窗，5-天窗，6-封闭窗，7-飘窗
        /// </summary>
        [Column("hasWindow")]
        [StringLength(15)]
        public int? hasWindow { get; set; } = 3;

        /// <summary>
        /// 海外酒店-面积
        /// </summary>
        [Column("areaRange")]
        [StringLength(15)]
        public string areaRange { get; set; } = "";

        /// <summary>
        /// 国内-加床
        /// -100-未知，-1-不可加床，0-免费加床，>0-加床收费
        /// </summary>
        public int? addBedFee { get; set; }


        /// <summary>
        /// 海外酒店- 对应 接口  basicRoomBedType 字段,接口传输时 需转换成List<BasicRoomBedType>类型
        /// </summary>
        [Column("basicRoomBedTypeListDesc")]
        [StringLength(1000)]
        public string basicRoomBedTypeListDesc { get; set; } = "";
        /// <summary>
        /// 无线网络
        /// 1：全部房间有无线网，免费
        /// 2：部分房间有无线网，免费
        /// 3：全部房间有无线网，收费
        /// 4：部分房间有无线网，收费
        /// </summary>
        public int? hasWirelessBroadnet { get; set; }
        /// <summary>
        /// 有线网络
        /// 1：全部房间有有线网，免费
        /// 2：部分房间有有线网，免费
        /// 3：全部房间有有线网，收费
        /// 4：部分房间有有线网，收费
        /// </summary>
        public int? hasWiredBroadnet { get; set; }

        /// <summary>
        /// 网络收费； 国内酒店网络收费时，不能为空
        /// </summary>
        [StringLength(300)]
        public string broadnetFeeDetail { get; set; } = "";

        /// <summary>
        /// 吸烟说明
        /// 1：可吸烟
        /// 2：禁烟
        /// 3：部分客房可吸烟
        /// </summary>
        public int? smoke { get; set; }


        [Column(TypeName = "DateTime")]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        [Column(TypeName = "MTBedType")]
        public int MTBedType { get; set; }
        /// <summary>
        /// 1有效,0无效
        /// </summary>
        public int Status { get; set; }
    }


}

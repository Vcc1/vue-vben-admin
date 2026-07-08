
namespace Infrastructures.RoomMatchingRedis.Entitys
{
    public class RoomMappingRedisModel
    {
        public long Id { get; set; } // 👈 必须加主键！

        public string MTHotelCode { get; set; }
        /// <summary>
        /// 供应商房型名称（中文或英文）
        /// </summary>
        public string SupRoomName { get; set; }

        /// <summary>
        /// 供应商床型键
        /// </summary>
        public string SupBedInfoKey { get; set; }

        /// <summary>
        /// 对应的美团房型ID（OTARoomId）
        /// </summary>
        public long MTRoomId { get; set; }

        /// <summary>
        /// 房型匹配状态
        /// </summary>
        public int MTRoomStatus { get; set; }

        /// <summary>
        /// 房型匹配类型
        /// </summary>
        public int MTRoomType { get; set; }

        /// <summary>
        /// 美团 OTARoomId
        /// </summary>
        public long OTARoomId { get; set; }

        /// <summary>
        /// 美团房型中文名
        /// </summary>
        public string OTARoomNameCN { get; set; }

        /// <summary>
        /// 美团房型英文名
        /// </summary>
        public string OTARoomNameEN { get; set; }

        /// <summary>
        /// 美团床型Key
        /// </summary>
        public string OTABedInfoKey { get; set; }

    }

}

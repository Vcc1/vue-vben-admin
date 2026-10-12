namespace Infrastructures.RoomMatchingRedis.Entitys
{
    public class HotelMappingDto
    {
        public int Id { get; set; }
        public string MTHotelCode { get; set; }


        public int SupRoomId { get; set; }
        public string SRoomName_CN { get; set; }
        public string SRoomName_EN { get; set; }
        public int? Status { get; set; }
        /// <summary>
        /// 来至SUP房型表
        /// </summary>
        public string BedInfoKey { get; set; }


    
        public int OTARoomId { get; set; }
        public string OTASRoomName_CN { get; set; }
        public string OTASRoomName_EN { get; set; }
        /// <summary>
        /// 来至OTA房型表
        /// </summary>
        public int MTBedType { get; set; }



        public int MatchType { get; set; }
        public string CreateBy { get; set; }
    }
}
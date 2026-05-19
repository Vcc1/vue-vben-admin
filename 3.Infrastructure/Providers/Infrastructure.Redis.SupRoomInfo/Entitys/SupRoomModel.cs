using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Redis.SupRoomInfo.Entitys
{
    public class SupRoomModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("RN")]
        public string RoomName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("REN")]
        public string RoomEnName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("HC")]
        public string HotelCode { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("MHC")]
        public string MTHotelCode { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("RC")]
        public string RoomCode { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("BT")]
        public string BedType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("BI")]
        public string BedInfo { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("BIK")]
        public string BedInfoKey { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("AF")]
        public int AcreageFrom { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("AT")]
        public int AcreageTo { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("ST")]
        public int SmokeType { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("MA")]
        public int MaxAdt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("SID")]
        public int SupId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("DT")]
        public string CreateTime { get; set; }
    }
}

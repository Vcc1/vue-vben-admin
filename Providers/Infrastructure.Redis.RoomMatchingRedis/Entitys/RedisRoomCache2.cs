using MemoryPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Infrastructures.RoomMatchingRedis.Entitys
{
    [MemoryPackable]
    public partial class RedisRoomCache2
    {
        /// <summary>
        /// 萌兔房型  Key 为 萌兔房型id  Value 为 萌兔兔房型所有数据
        /// </summary>
        [MemoryPackOrder(0)]
        [JsonProperty("M")]
        [JsonPropertyName("M")]
        public Dictionary<int, MTRoomModel2> MTRooms { get; set; } = new();

        /// <summary>
        /// Key1: RoomName Key2:BedInfoKey
        /// </summary>
        [MemoryPackOrder(1)]
        [JsonProperty("S")]
        [JsonPropertyName("S")]
        public Dictionary<string, Dictionary<string, SupRoomMapping2>> SupRooms { get; set; } = new();
    }

    [MemoryPackable]
    public partial class MTRoomModel2
    {
        [MemoryPackOrder(0)]
        [JsonProperty("R")]
        [JsonPropertyName("R")]
        public int OTARoomId { get; set; }

        [MemoryPackOrder(1)]
        [JsonProperty("C")]
        [JsonPropertyName("C")]
        public string RoomNameCN { get; set; }

        [MemoryPackOrder(2)]
        [JsonProperty("E")]
        [JsonPropertyName("E")]
        public string RoomNameEN { get; set; }

        [MemoryPackOrder(3)]
        [JsonProperty("B")]
        [JsonPropertyName("B")]
        public string BedInfoKey { get; set; }
    }

    [MemoryPackable]
    public partial class SupRoomMapping2
    {
        [MemoryPackOrder(0)]
        [JsonProperty("R")]
        [JsonPropertyName("R")]
        public int MTRoomId { get; set; }

        [MemoryPackOrder(1)]
        [JsonProperty("T")]
        [JsonPropertyName("T")]
        public int Type { get; set; }

        [MemoryPackOrder(2)]
        [JsonProperty("S")]
        [JsonPropertyName("S")]
        public int Status { get; set; }
    }
}

using Framework.Core.IOC;
using Infrastructures.RoomMatchingRedis.Entitys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VBaseFramework.Core.Redis;
using VBaseFramework.Core.String;

namespace Infrastructures.RoomMatchingRedis
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class RoomMappingRedis : RedisContextBase
    {
        private readonly ILogger<RoomMappingRedis> _logger;

        public RoomMappingRedis(IConfiguration configuration, ILogger<RoomMappingRedis> logger) : base(configuration, "RoomMappingRedis")
        {

            _logger = logger;
        }

        public void GetRoomMatching(string mtHotelCode)
        {
            try
            {
                var a1 = GetRoomMatching1(mtHotelCode);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{mtHotelCode}");
            }
        }

        private RedisRoomCache2 GetRoomMatching1(string mtHotelCode)
        {
            var cacheKey = $"RM_{mtHotelCode}";
            var room = DB.StringGet(cacheKey);
            if (room.IsNullOrEmpty)
            {
                return null;
            }
            var roomCache = StringCompression.DecompressString(room);
            var redisRoom = roomCache.FromJson<RedisRoomCache2>();
            return redisRoom;
        }

        /// <summary>
        /// 只读取 Redis 原始数据（同步），不执行解压和反序列化
        /// </summary>
        public RedisValue GetRawDataSync(string mtHotelCode)
        {
            var cacheKey = $"RM_{mtHotelCode}";
            var room = DB.StringGet(cacheKey);
            return room;
        }

        /// <summary>
        /// 只读取 Redis 原始数据（异步），不执行解压和反序列化
        /// </summary>
        public async Task<RedisValue> GetRawDataAsync(string mtHotelCode)
        {
            var cacheKey = $"RM_{mtHotelCode}";
            var room = await DB.StringGetAsync(cacheKey);
            return room;
        }

        public void Push(string hotelCode, List<HotelMappingDto> items)
        {
            try
            {
                var cacheKey = $"RM_{hotelCode}";
                if (items.Count == 0)
                {
                    DB.KeyDelete(cacheKey);
                    return;
                }

                var room = new RedisRoomCache2
                {
                    MTRooms = items
                      .OrderBy(o => o.OTARoomId)
                      .DistinctBy(d => d.OTARoomId)
                      .ToDictionary(o => o.OTARoomId, o => new MTRoomModel2()
                      {
                          OTARoomId = o.OTARoomId,
                          RoomNameCN = o.OTASRoomName_CN,
                          RoomNameEN = o.OTASRoomName_EN,
                          BedInfoKey = o.MTBedType switch { 1 => "6:1", 2 => "19:1", 3 => "6:1|19:1", 4 => "5:1", _ => "0:0" }
                      })
                };
                foreach (var mapRoom in items)
                {
                    var bedInfoKey = mapRoom.BedInfoKey;
                    var roomNameCN = mapRoom.SRoomName_CN?.Trim().ToLower();
                    var roomNameEN = mapRoom.SRoomName_EN?.Trim().ToLower();

                    var srm = new SupRoomMapping2()
                    {
                        Type = mapRoom.MatchType,
                        Status = mapRoom.Status ?? 0,
                        MTRoomId = mapRoom.OTARoomId
                    };
                    if (!string.IsNullOrWhiteSpace(roomNameCN))
                    {
                        room.SupRooms.TryAdd(roomNameCN, new());
                        room.SupRooms[roomNameCN].TryAdd(bedInfoKey, srm);
                    }



                    if (string.IsNullOrWhiteSpace(roomNameEN) || roomNameEN == roomNameCN)
                    {
                        continue;
                    }
                    room.SupRooms.TryAdd(roomNameEN, new());
                    room.SupRooms[roomNameEN].TryAdd(bedInfoKey, srm);
                }
                // 写入Redis 
                //_ = DB?.StringSetAsync(cacheKey, StringCompression.CompressString(room.ToJson(new JsonSerializerSettings
                //{

                //    Formatting = Formatting.None, // 不格式化，压缩输出
                //    ContractResolver = new DefaultContractResolver(), // 不做首字母小写，保留原始属性名
                //    NullValueHandling = NullValueHandling.Ignore
                //})), TimeSpan.FromDays(7)); 
                _ = DB?.StringSetAsync(cacheKey, StringCompression.CompressString(room.ToJson(new JsonSerializerOptions
                {
                    WriteIndented = false, // 不格式化，压缩输出
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // 首字母小写
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略 null 值
                    PropertyNameCaseInsensitive = true // 反序列化时忽略大小写
                })), TimeSpan.FromDays(7));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{hotelCode}");
            }

        }

        public List<string> Keys(string pattern = "*")
        {
            var server = RedisMultiplexer.GetServer(RedisMultiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).Select(x => x.ToString()).ToList();
            return keys;
        }

        public async Task VisitKeyStatisticsAsync(
            Func<RoomMappingRedisKeyStatistics, int, int, Task> visitor,
            string pattern = "RM_*",
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var keys = new List<RedisKey>();

            _logger.LogInformation("Room matching redis statistics reading keys started. Pattern={Pattern}", pattern);

            foreach (var endpoint in RedisMultiplexer.GetEndPoints())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var server = RedisMultiplexer.GetServer(endpoint);
                if (!server.IsConnected)
                {
                    _logger.LogWarning("Room matching redis endpoint skipped because it is not connected. Endpoint={Endpoint}", endpoint);
                    continue;
                }

                var endpointKeys = server.Keys(DB.Database, pattern: pattern, pageSize: 200).ToList();
                _logger.LogInformation(
                    "Room matching redis keys loaded from endpoint. Endpoint={Endpoint}, KeyCount={KeyCount}",
                    endpoint,
                    endpointKeys.Count);

                keys.AddRange(endpointKeys);
            }

            _logger.LogInformation("Room matching redis statistics reading keys completed. TotalKeyCount={KeyCount}", keys.Count);

            for (var index = 0; index < keys.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var redisKey = keys[index];

                try
                {
                    var key = redisKey.ToString();
                    var ttl = await DB.KeyTimeToLiveAsync(redisKey);
                    var value = await DB.StringGetAsync(redisKey);

                    RoomMappingRedisKeyStatistics stat;
                    if (value.IsNullOrEmpty)
                    {
                        stat = new RoomMappingRedisKeyStatistics(key, ttl, null, 0);
                    }
                    else
                    {
                        var roomCache = StringCompression.DecompressString(value);
                        var redisRoom = roomCache.FromJson<RedisRoomCache2>();
                        var itemCount = redisRoom?.SupRooms?.Sum(room => room.Value?.Count ?? 0) ?? 0;
                        DateTimeOffset? expireAtUtc = ttl.HasValue ? now.Add(ttl.Value) : null;
                        stat = new RoomMappingRedisKeyStatistics(key, ttl, expireAtUtc, itemCount);
                    }

                    await visitor(stat, index + 1, keys.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read room matching redis statistics for key {RedisKey}", redisKey);
                }
            }
        }

        public List<RoomMappingRedisModel> Get(string MTHotelCode)
        {
            var cacheKey = $"RM_{MTHotelCode}";
            var room = DB.StringGet(cacheKey);
            if (room.IsNullOrEmpty)
            {
                return null;
            }
            var roomCache = StringCompression.DecompressString(room);
            var redisRoom = roomCache.FromJson<RedisRoomCache2>();
            return redisRoom.SupRooms.SelectMany(e =>
            {
                var roomName = e.Key;
                return e.Value.ToList().Select(bedItem =>
                {
                    var otaRoom = redisRoom.MTRooms[bedItem.Value.MTRoomId];
                    return new RoomMappingRedisModel()
                    {
                        MTHotelCode = MTHotelCode,
                        SupRoomName = roomName,
                        SupBedInfoKey = bedItem.Key,
                        MTRoomId = bedItem.Value.MTRoomId,
                        MTRoomStatus = bedItem.Value.Status,
                        MTRoomType = bedItem.Value.Type,

                        OTARoomId = otaRoom.OTARoomId,
                        OTARoomNameEN = otaRoom.RoomNameEN,
                        OTARoomNameCN = otaRoom.RoomNameCN,
                        OTABedInfoKey = otaRoom.BedInfoKey
                    };
                });
            }).ToList();

        }

        public async Task<RedisRoomCache2> GetSourceAsync(string MTHotelCode)
        {
            var cacheKey = $"RM_{MTHotelCode}";
            var room = await DB.StringGetAsync(cacheKey);
            if (room.IsNullOrEmpty)
            {
                return null;
            }
            var roomCache = StringCompression.DecompressString(room);
            var redisRoom = roomCache.FromJson<RedisRoomCache2>();
            return redisRoom;

        }

    }

    public sealed record RoomMappingRedisKeyStatistics(
        string Key,
        TimeSpan? TimeToLive,
        DateTimeOffset? ExpireAtUtc,
        int ItemCount);
}

using Framework.Core.IOC;
using Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery;
using Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VBaseFramework.Core.Redis;
using VBaseFramework.Core.String;

namespace Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class SupRoomInfoDBContext : RedisContextBase
    {
        private readonly ILogger<SupRoomInfoDBContext> _logger;
        public SupRoomInfoDBContext(IConfiguration configuration, ILogger<SupRoomInfoDBContext> logger) : base(configuration, "SupRoomInfoRedisDirectPriceDiscovery")
        {

            _logger = logger;
        }
        static int pageindex = 0;
        string SupRoomInfoKey = "NewSupRoom";

        public long GetListLength(string key)
        {
            return DB.ListLength(key);
        }

        public async Task<long> GetSetLengthAsync(string key)
        {
            return await DB.SetLengthAsync(key);
        }

        public async Task<RedisValue[]> SetPopAsync(string key, long count)
        {
            return await DB.SetPopAsync(key, count);
        }

        public async Task<IReadOnlyList<string>> GetSetKeysByPrefixAsync(string prefix, CancellationToken cancellationToken)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var pattern = $"{prefix}*";
            var database = DB.Database;

            foreach (var endpoint in RedisMultiplexer.GetEndPoints())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var server = RedisMultiplexer.GetServer(endpoint);
                if (!server.IsConnected)
                {
                    continue;
                }

                foreach (var redisKey in server.Keys(database, pattern: pattern, pageSize: 200))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await DB.KeyTypeAsync(redisKey) != RedisType.Set)
                    {
                        continue;
                    }

                    keys.Add(redisKey.ToString());
                }
            }

            return keys.ToList();
        }

        public List<TempSupRoomInfo2> GetSupRoomInfo(int maxSize, CancellationToken ct)
        {
            // 从 Redis 取数据
            var result = new List<TempSupRoomInfo2>();
            var values = new List<RedisValue>();
            try
            {
                //values = DB.ListRightPop(SupRoomInfoKey, maxSize);
                for (int i = 0; i < maxSize && !ct.IsCancellationRequested; i++)
                {
                    var item = DB.ListRightPop(SupRoomInfoKey);
                    if (item.IsNullOrEmpty) break; // 列表没了就退出
                    values.Add(item);
                }
                //values = DB.ListRange(SupRoomInfoKey, pageindex, (pageindex + maxSize) - 1).ToList();
                pageindex += maxSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis 读取异常");
                return result;
            }
            if (values == null)
            {
                return result;
            }
            foreach (var value in values)
            {
                try
                {
                    _logger.LogTrace(value);
                    var e = JsonSerializer.Deserialize<SupRoomModel>(value.ToString());
                    if (e != null)
                    {
                        result.Add(new TempSupRoomInfo2()
                        {
                            AcreageFrom = e.AcreageFrom,
                            AcreageTo = e.AcreageTo,
                            BedInfo = e.BedInfo,
                            BedInfoKey = e.BedInfoKey,
                            MaxAdt = e.MaxAdt,
                            MaxChd = null,
                            SRoomCode = e.RoomCode,
                            SRoomNameCN = e.RoomName,
                            SRoomNameEN = e.RoomEnName,
                            MTHotelCode = e.MTHotelCode,
                            SmokeType = e.SmokeType,
                            SHotelCode = e.HotelCode,
                            SupId = e.SupId,
                            Status = 1,
                            CreaterBy = "System:RedisToSql",
                            CreaterDate = DateTime.Now,
                        });
                    }
                    else
                    {
                        // 反序列化失败日志
                        _logger.LogError("消费失败 | 无法解析消息: {0}", value);
                    }
                }
                catch (Exception ex)
                {
                    // 异常日志
                    _logger.LogError(ex, "消费异常 | 原始消息: {0}", value);
                }
            }
            // 确保日志落盘
            LogManager.Flush();
            return result;
        }
    }
}

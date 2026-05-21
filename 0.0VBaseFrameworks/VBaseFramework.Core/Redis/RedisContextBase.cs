using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.Redis
{
    /// <summary>
    /// Redis 仓储模式基类
    /// </summary>
    public class RedisContextBase
    {
        //IConfiguration configuration
        protected ConnectionMultiplexer RedisMultiplexer { get; private set; }
        protected IDatabase DB { get; private set; }
        private string ConnString;
        public RedisContextBase(IConfiguration configuration, string RedisConnectionKey)
        {
            CreateDb(configuration, RedisConnectionKey);
            ChangeToken.OnChange(
                () => configuration.GetReloadToken(),
                () => CreateDb(configuration, RedisConnectionKey));
        }

        private void CreateDb(IConfiguration configuration, string RedisConnectionKey)
        {
            var conn = configuration.GetConnectionString(RedisConnectionKey);
            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new Exception($"Redis:{RedisConnectionKey} 未配置连接字符串");
            }
            lock (this)
            {
                conn = configuration.GetConnectionString(RedisConnectionKey);
                if (string.IsNullOrWhiteSpace(conn))
                {
                    throw new Exception($"Redis:{RedisConnectionKey} 未配置连接字符串");
                }

                if (ConnString == conn)
                {
                    return;
                }
                //连接redis服务器
                string redisConnection = NormalizeRedisConnectionString(conn);
                RedisMultiplexer = ConnectionMultiplexer.Connect(redisConnection);
                DB = RedisMultiplexer.GetDatabase();
                ConnString = conn;
            }

        }

        private static string NormalizeRedisConnectionString(string redisConnection)
        {
            if (string.IsNullOrWhiteSpace(redisConnection))
            {
                return redisConnection;
            }

            var segments = redisConnection
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(segment =>
                {
                    if (!segment.Contains('=', StringComparison.Ordinal))
                    {
                        return true;
                    }

                    var parts = segment.Split('=', 2);
                    return !(parts[0].Equals("ssl", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(parts[1]));
                });

            return string.Join(",", segments);
        }
    }
}

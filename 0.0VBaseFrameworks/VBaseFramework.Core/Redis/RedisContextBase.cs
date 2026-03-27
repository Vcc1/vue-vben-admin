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
            if (ConnString == conn)
            {
                throw new Exception($"Redis:{RedisConnectionKey} 未配置连接字符串");
                return;
            }
            lock (this)
            {
                conn = configuration.GetConnectionString(RedisConnectionKey);
                if (ConnString == conn)
                {
                    return;
                }
                //连接redis服务器
                string redisConnection = configuration.GetConnectionString(RedisConnectionKey);
                RedisMultiplexer = ConnectionMultiplexer.Connect(redisConnection);
                DB = RedisMultiplexer.GetDatabase();
            }

        }
    }
}

using Framework.Core.IOC;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace Infrastructure.Redis.SupRoomInfo.CachePriceDiscovery;

[Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
public sealed class CachePriceDiscoveryRedisContext : RedisContextBase
{
    public CachePriceDiscoveryRedisContext(IConfiguration configuration)
        : base(configuration, "SupRoomInfoRedisCachePriceDiscovery")
    {
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
}

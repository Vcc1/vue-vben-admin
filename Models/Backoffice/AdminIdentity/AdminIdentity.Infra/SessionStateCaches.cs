using System.Text.Json;
using AdminIdentity.App;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AdminIdentity.Infra;

/// <summary>
/// Redis 实现。所有失败都吞掉并退回 DB：缓存是加速器，不是权威。
/// 唯一按严重级别记录的是吊销标记写入失败——那会让被踢的凭证在缓存 TTL 内继续可用。
/// </summary>
internal sealed class RedisSessionStateCache(
    AdminIdentityRedisContext redis,
    ILogger<RedisSessionStateCache> logger) : ISessionStateCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SessionState?> TryGetAsync(string sid, CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.Database.StringGetAsync(AdminIdentityRedisContext.SessionKey(sid));
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<SessionState>(value.ToString(), JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "读取会话缓存失败，回退数据库查询。sid={Sid}", sid);
            return null;
        }
    }

    public async Task SetAsync(SessionState state, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }
        try
        {
            await redis.Database.StringSetAsync(
                AdminIdentityRedisContext.SessionKey(state.Sid),
                JsonSerializer.Serialize(state, JsonOptions),
                ttl);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "写入会话缓存失败，下次请求将直查数据库。sid={Sid}", state.Sid);
        }
    }

    public async Task RemoveAsync(string sid, CancellationToken cancellationToken)
    {
        try
        {
            await redis.Database.KeyDeleteAsync(AdminIdentityRedisContext.SessionKey(sid));
        }
        catch (Exception ex) when (ex is RedisException)
        {
            logger.LogWarning(ex, "清除会话缓存失败，将依赖 TTL 过期。sid={Sid}", sid);
        }
    }

    public async Task MarkRevokedAsync(string sid, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }
        try
        {
            await redis.Database.StringSetAsync(AdminIdentityRedisContext.SessionRevokedKey(sid), "1", ttl);
        }
        catch (Exception ex) when (ex is RedisException)
        {
            // 标记没写上，被踢的凭证会在事实缓存 TTL 内继续可用（DB 仍然会在下一次未命中时拦住它）。
            logger.LogCritical(ex, "写入会话吊销标记失败，秒踢在该副本上可能延迟生效。sid={Sid}", sid);
        }
    }

    public async Task<bool> IsRevokedAsync(string sid, CancellationToken cancellationToken)
    {
        try
        {
            return await redis.Database.KeyExistsAsync(AdminIdentityRedisContext.SessionRevokedKey(sid));
        }
        catch (Exception ex) when (ex is RedisException)
        {
            // 查不到就当没吊销，交给 DB 兜底——DB 是权威，不会因为 Redis 故障而漏判。
            logger.LogWarning(ex, "查询会话吊销标记失败，回退数据库判定。sid={Sid}", sid);
            return false;
        }
    }
}

/// <summary>未配置 Redis 时的空实现：永远未命中，会话校验退回每请求直查数据库。</summary>
internal sealed class NullSessionStateCache : ISessionStateCache
{
    public Task<SessionState?> TryGetAsync(string sid, CancellationToken cancellationToken) =>
        Task.FromResult<SessionState?>(null);

    public Task SetAsync(SessionState state, TimeSpan ttl, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveAsync(string sid, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MarkRevokedAsync(string sid, TimeSpan ttl, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> IsRevokedAsync(string sid, CancellationToken cancellationToken) => Task.FromResult(false);
}

using System.Collections.Concurrent;
using System.Text.Json;
using AdminIdentity.App;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AdminIdentity.Infra;

/// <summary>
/// Redis 实现：验证码与挑战状态跨副本共享，进程重启也不会把用户卡在验证流程中间。
/// 与 <see cref="RedisSessionStateCache"/> 不同，这里没有 DB 兜底——读不到就是读不到，
/// 因此 Redis 故障会直接表现为"验证码已失效，请重新获取"，而不是静默放行。
/// </summary>
internal sealed class RedisStepUpStateStore(
    AdminIdentityRedisContext redis,
    ILogger<RedisStepUpStateStore> logger) : IStepUpStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StepUpChallengeState?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.Database.StringGetAsync(AdminIdentityRedisContext.StepUpKey(key));
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<StepUpChallengeState>(value.ToString(), JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogError(ex, "读取二次验证状态失败。key={Key}", key);
            return null;
        }
    }

    public async Task SetAsync(string key, StepUpChallengeState state, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (ttl <= TimeSpan.Zero)
        {
            await RemoveAsync(key, cancellationToken);
            return;
        }
        try
        {
            await redis.Database.StringSetAsync(
                AdminIdentityRedisContext.StepUpKey(key),
                JsonSerializer.Serialize(state, JsonOptions),
                ttl);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogError(ex, "写入二次验证状态失败。key={Key}", key);
            throw new StepUpException(StepUpException.InvalidToken, "验证服务暂不可用，请稍后重试。");
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await redis.Database.KeyDeleteAsync(AdminIdentityRedisContext.StepUpKey(key));
        }
        catch (Exception ex) when (ex is RedisException)
        {
            logger.LogWarning(ex, "清除二次验证状态失败，将依赖 TTL 过期。key={Key}", key);
        }
    }
}

/// <summary>
/// 未配置 Redis 时的进程内实现（单副本可用）。
/// 挑战本身有 TTL 语义，这里靠 <see cref="Purge"/> 在每次写入时顺带清理过期项。
/// </summary>
internal sealed class MemoryStepUpStateStore(TimeProvider timeProvider) : IStepUpStateStore
{
    private static readonly ConcurrentDictionary<string, StepUpChallengeState> Store =
        new(StringComparer.Ordinal);

    public Task<StepUpChallengeState?> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (!Store.TryGetValue(key, out var state))
        {
            return Task.FromResult<StepUpChallengeState?>(null);
        }
        if (state.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            Store.TryRemove(key, out _);
            return Task.FromResult<StepUpChallengeState?>(null);
        }
        return Task.FromResult<StepUpChallengeState?>(state);
    }

    public Task SetAsync(string key, StepUpChallengeState state, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (ttl <= TimeSpan.Zero)
        {
            Store.TryRemove(key, out _);
            return Task.CompletedTask;
        }
        Store[key] = state;
        Purge();
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        Store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void Purge()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in Store)
        {
            if (entry.Value.ExpiresAtUtc <= now)
            {
                Store.TryRemove(entry.Key, out _);
            }
        }
    }
}

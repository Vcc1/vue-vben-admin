using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using VBaseFramework.Core.Redis;

namespace Infrastructure.Redis.SupPriceCahce;

public sealed class SupPriceCahceRedis : RedisContextBase
{
    public SupPriceCahceRedis(IConfiguration configuration)
        : base(configuration, "SupPriceCahceRedis")
    {
    }

    public Task SavePricePayloadAsync(
        string redisKey,
        string hashField,
        string payload,
        TimeSpan expiry)
    {
        return Task.WhenAll(
            DB.HashSetAsync(redisKey, hashField, payload),
            DB.KeyExpireAsync(redisKey, expiry));
    }

    public async Task<HashSet<string>> GetChargeTitlesAsync()
    {
        var values = await DB.SetMembersAsync("chargestitle");
        return values
            .Select(value => value.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task AddChargeTitlesAsync(IReadOnlyCollection<string> values)
    {
        return AddSetMembersAsync("chargestitle", values);
    }

    public async Task<HashSet<string>> GetUnknownPropertiesAsync()
    {
        var values = await DB.SetMembersAsync("unknown_properties");
        return values
            .Select(value => value.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task AddUnknownPropertiesAsync(IReadOnlyCollection<string> values)
    {
        return AddSetMembersAsync("unknown_properties", values);
    }

    private Task AddSetMembersAsync(string key, IReadOnlyCollection<string> values)
    {
        if (values.Count == 0)
        {
            return Task.CompletedTask;
        }

        var redisValues = values
            .Select(value => (RedisValue)value)
            .ToArray();
        return DB.SetAddAsync(key, redisValues);
    }
}
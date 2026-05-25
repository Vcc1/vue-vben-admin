using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.SupRoomInfoIngestion;

public sealed class RedisSupRoomInfoSetReader : ISupRoomInfoSetReader
{
    static readonly Microsoft.Extensions.Caching.Memory.MemoryCache DedupCache = new(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions
    {
        SizeLimit = 2_000_000,

    });
    static readonly TimeSpan DedupCacheTtl = TimeSpan.FromDays(7);
    static long _dedupCacheCount;

    readonly CachePriceDiscoveryRedisContext _redis;
    readonly IBedInfoRestoreStep _bedInfoRestoreStep;
    readonly SupRoomInfoIngestionMetrics _metrics;
    readonly ILogger<RedisSupRoomInfoSetReader> _logger;

    public RedisSupRoomInfoSetReader(
        CachePriceDiscoveryRedisContext redis,
        IBedInfoRestoreStep bedInfoRestoreStep,
        SupRoomInfoIngestionMetrics metrics,
        ILogger<RedisSupRoomInfoSetReader> logger)
    {
        _redis = redis;
        _bedInfoRestoreStep = bedInfoRestoreStep;
        _metrics = metrics;
        _logger = logger;
        _metrics.SetDedupCacheCount(Volatile.Read(ref _dedupCacheCount));
    }

    public async Task<IReadOnlyList<TempSupRoomInfo2>> ReadAsync(
        string setKeyPrefix,
        int fetchBatchSize,
        CancellationToken cancellationToken)
    {
        var items = new List<TempSupRoomInfo2>();
        var setKeys = await _redis.GetSetKeysByPrefixAsync(setKeyPrefix, cancellationToken);
        foreach (var setKey in setKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = await _redis.SetPopAsync(setKey, fetchBatchSize);
            if (values.Length == 0)
            {
                continue;
            }

            foreach (var value in values)
            {
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                try
                {
                    if (!CachePriceDiscoveryRoomParser.TryParse(value.ToString(), out var raw) || raw is null)
                    {
                        continue;
                    }

                    if (!TryResolveSupId(setKey, setKeyPrefix, out var supId))
                    {
                        _metrics.RecordError("set_sup_id_parse");
                        _logger.LogWarning("无法从 Set key 解析 SupId。key={SetKey}", setKey);
                        continue;
                    }

                    var dedupKey = BuildDedupCacheKey(setKey, value.ToString());
                    if (DedupCache.TryGetValue(dedupKey, out _))
                    {
                        _metrics.RecordDedupCacheHit();
                        _metrics.SetDedupCacheCount(Volatile.Read(ref _dedupCacheCount));
                        continue;
                    }

                    _metrics.RecordDedupCacheMiss();

                    DedupCache.Set(
                        dedupKey,
                        true,
                        CreateDedupCacheEntryOptions());
                    _metrics.SetDedupCacheCount(Interlocked.Increment(ref _dedupCacheCount));

                    items.Add(new TempSupRoomInfo2
                    {
                        BedInfoKey = raw.BedInfoKey,
                        BedInfo = _bedInfoRestoreStep.Restore(raw.BedInfoKey),
                        SRoomCode = string.Empty,
                        SRoomNameCN = raw.RoomName ?? string.Empty,
                        SRoomNameEN = string.Empty,
                        MTHotelCode = raw.MTHotelCode,
                        SHotelCode = raw.SupHotelCode,
                        SupId = supId,
                        Status = 1,
                        CreaterBy = "System:SetRedisToSql",
                        CreaterDate = DateTime.Now,
                    });
                }
                catch (Exception ex)
                {
                    _metrics.RecordError("set_deserialize");
                    _logger.LogError(ex, "解析 Set 源 SupRoomInfo 失败。key={SetKey}", setKey);
                }
            }

            if (items.Count >= fetchBatchSize)
            {
                break;
            }
        }

        return items;
    }

    public async Task<long> GetPendingCountAsync(string setKeyPrefix, CancellationToken cancellationToken)
    {
        long count = 0;
        var setKeys = await _redis.GetSetKeysByPrefixAsync(setKeyPrefix, cancellationToken);
        foreach (var setKey in setKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count += await _redis.GetSetLengthAsync(setKey);
        }

        return count;
    }

    static bool TryResolveSupId(string setKey, string prefix, out int supId)
    {
        supId = 0;
        if (string.IsNullOrWhiteSpace(setKey) || !setKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = setKey[prefix.Length..].TrimStart(':', '_', '-');
        return int.TryParse(suffix, out supId);
    }

    static string BuildDedupCacheKey(string setKey, string value)
    {
        return $"{setKey}:{value}";
    }

    MemoryCacheEntryOptions CreateDedupCacheEntryOptions()
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DedupCacheTtl,
            Size = 1,
        }.RegisterPostEvictionCallback((_, _, _, _) =>
        {
            while (true)
            {
                var current = Volatile.Read(ref _dedupCacheCount);
                var next = Math.Max(0, current - 1);
                if (Interlocked.CompareExchange(ref _dedupCacheCount, next, current) == current)
                {
                    _metrics.SetDedupCacheCount(next);
                    return;
                }
            }
        });
    }
}

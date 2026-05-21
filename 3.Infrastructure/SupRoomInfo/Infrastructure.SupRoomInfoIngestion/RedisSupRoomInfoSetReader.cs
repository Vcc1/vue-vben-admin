namespace Infrastructure.SupRoomInfoIngestion;

public sealed class RedisSupRoomInfoSetReader : ISupRoomInfoSetReader
{
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
}

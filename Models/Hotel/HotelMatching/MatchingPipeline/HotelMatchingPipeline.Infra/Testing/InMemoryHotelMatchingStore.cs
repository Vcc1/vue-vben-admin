using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.Infra.Testing;

public sealed class InMemoryHotelMatchingStore : IHotelCleaningRepository, IHotelMatchingRepository, IHotelMatchDeliveryRepository
{
    readonly List<RawHotelData> _raw = [];
    readonly Dictionary<string, HotelCleaningResult> _cleaned = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HotelMatchStatus> _matchStatuses = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HotelMatchDeliveryReceipt> _deliveries = new(StringComparer.Ordinal);
    readonly HashSet<string> _sending = new(StringComparer.Ordinal);

    public List<HotelMatchDeliveryCommand> DeliveryCommands { get; } = [];

    public void AddRaw(RawHotelData raw) => _raw.Add(raw);

    public async Task<HotelCleaningWorkSet> LoadCleaningWorkSetAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        int pendingIdBatchSize,
        Action<HotelCleaningScanProgress> reportScanProgress,
        Func<int, CancellationToken, ValueTask> enqueuePendingId,
        CancellationToken cancellationToken)
    {
        var rawRows = _raw.Where(x => x.Type == type).ToArray();
        var pendingIndexes = rawRows
            .Select((raw, index) => (raw, dataId: index + 1))
            .Where(x => !_cleaned.TryGetValue(Key(x.raw.Type, x.raw.SupplierId, x.raw.HotelCode), out var value)
                || value.CleanVersion != cleanVersion
                || value.RegionBoundaryVersion != regionBoundaryVersion
                || value.SourceDataHash != (x.raw.SourceDataHash ?? HotelDataHasher.HashRaw(x.raw)))
            .Select(x => x.dataId)
            .ToArray();
        var cleaned = rawRows.LongCount(x => _cleaned.TryGetValue(Key(x.Type, x.SupplierId, x.HotelCode), out var value)
            && value.CleanVersion == cleanVersion
            && value.RegionBoundaryVersion == regionBoundaryVersion
            && value.SourceDataHash == (x.SourceDataHash ?? HotelDataHasher.HashRaw(x)));
        reportScanProgress(new HotelCleaningScanProgress(rawRows.LongLength, 0, cleaned, 0, pendingIndexes.LongLength));
        var scanned = 0L;
        foreach (var pendingIndex in pendingIndexes)
        {
            await enqueuePendingId(pendingIndex, cancellationToken);
            scanned++;
        }

        reportScanProgress(new HotelCleaningScanProgress(rawRows.LongLength, rawRows.LongLength, cleaned, 0, pendingIndexes.LongLength));

        return new HotelCleaningWorkSet(rawRows.LongLength, cleaned, 0);
    }

    public Task<IReadOnlyList<RawHotelData>> GetHotelsByIdsAsync(
        HotelDataType type,
        IReadOnlyCollection<int> dataIds,
        CancellationToken cancellationToken)
    {
        var rawRows = _raw.Where(x => x.Type == type).ToArray();
        var result = dataIds
            .Where(dataId => dataId > 0 && dataId <= rawRows.Length)
            .Select(dataId => rawRows[dataId - 1])
            .ToArray();
        return Task.FromResult<IReadOnlyList<RawHotelData>>(result);
    }

    public Task<int> SaveCleanedHotelsAsync(IReadOnlyCollection<HotelCleaningResult> results, CancellationToken cancellationToken)
    {
        foreach (var result in results)
        {
            var key = Key(result.Raw.Type, result.Raw.SupplierId, result.Raw.HotelCode);
            var changed = !_cleaned.TryGetValue(key, out var previous)
                || previous.NormalizedDataHash != result.NormalizedDataHash;
            _cleaned[key] = result;
            if (result.Raw.Type == HotelDataType.Supplier && changed && result.IsMatchable)
            {
                _matchStatuses[key] = HotelMatchStatus.Pending;
            }
            else if (!result.IsMatchable)
            {
                _matchStatuses[key] = HotelMatchStatus.DataProblem;
            }
        }

        return Task.FromResult(results.Count);
    }

    public Task RecordCleaningFailuresAsync(
        IReadOnlyCollection<HotelCleaningFailureRecord> failures,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IReadOnlyList<HotelMatchWorkItem>> ClaimUnmatchedSupplierHotelsAsync(
        int matchingVersion,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var result = _cleaned.Values
            .Where(x => x.Raw.Type == HotelDataType.Supplier && x.IsMatchable)
            .Where(x => string.IsNullOrWhiteSpace(x.Cleaned.MtHotelCode))
            .Where(x => !string.IsNullOrWhiteSpace(x.Cleaned.RegionLevel1Code))
            .Where(x => HotelRecallPolicy.IsMatchingEnabled(
                x.Cleaned.RegionLevel1Code))
            .Where(x => _matchStatuses.GetValueOrDefault(Key(x.Raw.Type, x.Raw.SupplierId, x.Raw.HotelCode), HotelMatchStatus.Pending) == HotelMatchStatus.Pending)
            .Take(batchSize)
            .Select(x => new HotelMatchWorkItem(
                x.Cleaned,
                x.Raw.SourceDataVersion,
                x.NormalizedDataHash,
                x.CleanVersion))
            .ToArray();
        foreach (var item in result)
        {
            _matchStatuses[Key(HotelDataType.Supplier, item.Hotel.SupplierId, item.Hotel.HotelCode)] = HotelMatchStatus.Processing;
        }

        return Task.FromResult<IReadOnlyList<HotelMatchWorkItem>>(result);
    }

    public Task<IReadOnlyList<CleanedHotel>> RecallOtaCandidatesAsync(
        HotelMatchWorkItem supplier,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (!HotelRecallPolicy.IsMatchingEnabled(
                supplier.Hotel.RegionLevel1Code))
        {
            return Task.FromResult<IReadOnlyList<CleanedHotel>>([]);
        }

        var recall = HotelRecallPolicy.Resolve(
            supplier.Hotel.RegionLevel1Code,
            supplier.Hotel.CountryCode,
            radiusMeters,
            maxCandidates);
        var result = _cleaned.Values
            .Where(x => x.Raw.Type == HotelDataType.Platform
                && x.Raw.Estate == 1
                && x.IsMatchable)
            .Where(x => HotelRecallPolicy.IsMatchingEnabled(
                x.Cleaned.RegionLevel1Code))
            .Select(x => (x.Cleaned, DistanceMeters(supplier.Hotel, x.Cleaned)))
            .Where(x => !double.IsNaN(x.Item2) && x.Item2 <= recall.RadiusMeters)
            .OrderBy(x => x.Item2)
            .Take(recall.MaxCandidates ?? int.MaxValue)
            .Select(x => x.Cleaned)
            .ToArray();
        return Task.FromResult<IReadOnlyList<CleanedHotel>>(result);
    }

    public Task<IReadOnlyList<CleanedHotel>> RecallSupplierCandidatesAsync(
        CleanedHotel ota,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (!HotelRecallPolicy.IsMatchingEnabled(ota.RegionLevel1Code))
        {
            return Task.FromResult<IReadOnlyList<CleanedHotel>>([]);
        }

        var recall = HotelRecallPolicy.Resolve(
            ota.RegionLevel1Code,
            ota.CountryCode,
            radiusMeters,
            maxCandidates);
        var result = _cleaned.Values
            .Where(x => x.Raw.Type == HotelDataType.Supplier && x.IsMatchable)
            .Where(x => HotelRecallPolicy.IsMatchingEnabled(
                x.Cleaned.RegionLevel1Code))
            .Select(x => (x.Cleaned, DistanceMeters(ota, x.Cleaned)))
            .Where(x => !double.IsNaN(x.Item2) && x.Item2 <= recall.RadiusMeters)
            .OrderBy(x => x.Item2)
            .Take(recall.MaxCandidates ?? int.MaxValue)
            .Select(x => x.Cleaned)
            .ToArray();
        return Task.FromResult<IReadOnlyList<CleanedHotel>>(result);
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetTrustedAliasesAsync(
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken)
    {
        var wanted = mtHotelCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = _cleaned.Values
            .Where(x => x.Raw.Type == HotelDataType.Supplier && x.IsMatchable && !string.IsNullOrWhiteSpace(x.Cleaned.MtHotelCode))
            .Where(x => wanted.Contains(x.Cleaned.MtHotelCode!))
            .GroupBy(x => x.Cleaned.MtHotelCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<string>)x.Select(y => y.Cleaned.HotelName).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(result);
    }

    public Task CompleteMatchAsync(
        HotelMatchWorkItem workItem,
        HotelMatchDecision decision,
        int matchingVersion,
        string? error,
        DateTimeOffset? nextMatchTime,
        CancellationToken cancellationToken)
    {
        var key = Key(HotelDataType.Supplier, workItem.Hotel.SupplierId, workItem.Hotel.HotelCode);
        _matchStatuses[key] = decision.Kind switch
        {
            HotelMatchDecisionKind.AutoMatched => HotelMatchStatus.Delivered,
            HotelMatchDecisionKind.NeedsReview => HotelMatchStatus.NeedsReview,
            HotelMatchDecisionKind.DataProblem => HotelMatchStatus.DataProblem,
            _ when error is not null => HotelMatchStatus.Failed,
            _ => HotelMatchStatus.NoMatch
        };
        return Task.CompletedTask;
    }

    public Task<HotelMatchWorkItem?> GetSupplierHotelAsync(int supId, string supplierHotelCode, CancellationToken cancellationToken)
    {
        var result = _cleaned.Values.FirstOrDefault(x => x.Raw.Type == HotelDataType.Supplier
            && x.Raw.SupplierId == supId
            && string.Equals(x.Raw.HotelCode, supplierHotelCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result is null
            ? null
            : new HotelMatchWorkItem(result.Cleaned, result.Raw.SourceDataVersion, result.NormalizedDataHash, result.CleanVersion));
    }

    public Task<CleanedHotel?> GetOtaHotelAsync(string mtHotelCode, CancellationToken cancellationToken)
        => Task.FromResult(_cleaned.Values.FirstOrDefault(x => x.Raw.Type == HotelDataType.Platform
            && x.Raw.Estate == 1
            && string.Equals(x.Cleaned.HotelCode, mtHotelCode, StringComparison.OrdinalIgnoreCase))?.Cleaned);

    public Task<bool> IsVetoedAsync(
        int supId,
        string supplierHotelCode,
        string mtHotelCode,
        CancellationToken cancellationToken)
        => Task.FromResult(false);

    public Task<bool> TryCreateAsync(HotelMatchDeliveryCommand command, CancellationToken cancellationToken)
    {
        if (_deliveries.TryGetValue(command.IdempotencyKey, out var existingReceipt))
        {
            if (!existingReceipt.Success)
            {
                _deliveries.Remove(command.IdempotencyKey);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        if (DeliveryCommands.Any(x => x.IdempotencyKey == command.IdempotencyKey))
        {
            return Task.FromResult(false);
        }

        DeliveryCommands.Add(command);
        return Task.FromResult(true);
    }

    public Task MarkSendingAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _sending.Add(idempotencyKey);
        return Task.CompletedTask;
    }

    public Task MarkCompletedAsync(string idempotencyKey, HotelMatchDeliveryReceipt receipt, DateTimeOffset? nextRetryTime, CancellationToken cancellationToken)
    {
        _deliveries[idempotencyKey] = receipt;
        return Task.CompletedTask;
    }

    static string Key(HotelDataType type, int? supplierId, string hotelCode)
        => $"{type}:{supplierId?.ToString() ?? "0"}:{hotelCode}";

    static double DistanceMeters(CleanedHotel left, CleanedHotel right)
    {
        if (!left.Latitude.HasValue || !left.Longitude.HasValue || !right.Latitude.HasValue || !right.Longitude.HasValue)
        {
            return double.NaN;
        }

        const double radius = 6371008.8;
        var lat1 = (double)left.Latitude.Value * Math.PI / 180;
        var lat2 = (double)right.Latitude.Value * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = ((double)right.Longitude.Value - (double)left.Longitude.Value) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public sealed class InMemoryThirdPartyHotelMatchClient : IThirdPartyHotelMatchClient
{
    public bool ShouldSucceed { get; set; } = true;

    public List<HotelMatchDeliveryCommand> Calls { get; } = [];

    public Task<HotelMatchDeliveryReceipt> DeliverAsync(HotelMatchDeliveryCommand command, CancellationToken cancellationToken)
    {
        Calls.Add(command);
        return Task.FromResult(ShouldSucceed
            ? new HotelMatchDeliveryReceipt(true, "ok", null)
            : new HotelMatchDeliveryReceipt(false, null, "fake_delivery_failure"));
    }
}

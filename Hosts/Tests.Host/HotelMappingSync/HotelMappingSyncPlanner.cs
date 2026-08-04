namespace Tests.Host.HotelMappingSync;

public static class HotelMappingSyncPlanner
{
    public static IReadOnlyDictionary<string, string> BuildDesiredMappings(
        IEnumerable<HotelMappingSourceRow> sourceRows)
    {
        ArgumentNullException.ThrowIfNull(sourceRows);

        var desired = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in sourceRows)
        {
            var mtHotelCode = row.MTHotelCode?.Trim();
            if (string.IsNullOrWhiteSpace(mtHotelCode))
            {
                continue;
            }

            var supplierHotelCode = row.SHotelCode?.Trim();
            if (string.IsNullOrWhiteSpace(supplierHotelCode))
            {
                throw new InvalidOperationException(
                    $"SupHotelInfo contains an empty SHotelCode for SupId {row.SupId}.");
            }

            var field = $"{row.SupId}_{supplierHotelCode}";
            if (!desired.TryAdd(field, mtHotelCode))
            {
                throw new InvalidOperationException(
                    $"Duplicate hotel mapping field after normalization: {field}.");
            }
        }

        return desired;
    }

    public static HotelMappingSyncPlan CreatePlan(
        IReadOnlyDictionary<string, string> desired,
        IReadOnlyDictionary<string, string> current)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(current);

        var added = new List<HotelMappingSyncOperation>();
        var updated = new List<HotelMappingSyncOperation>();
        var deleted = new List<HotelMappingSyncOperation>();

        foreach (var pair in desired.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!current.TryGetValue(pair.Key, out var currentValue))
            {
                added.Add(new HotelMappingSyncOperation(
                    HotelMappingSyncOperationType.Add,
                    pair.Key,
                    null,
                    pair.Value));
            }
            else if (!string.Equals(currentValue, pair.Value, StringComparison.Ordinal))
            {
                updated.Add(new HotelMappingSyncOperation(
                    HotelMappingSyncOperationType.Update,
                    pair.Key,
                    currentValue,
                    pair.Value));
            }
        }

        foreach (var pair in current.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!desired.ContainsKey(pair.Key))
            {
                deleted.Add(new HotelMappingSyncOperation(
                    HotelMappingSyncOperationType.Delete,
                    pair.Key,
                    pair.Value,
                    null));
            }
        }

        return new HotelMappingSyncPlan(added, updated, deleted);
    }
}

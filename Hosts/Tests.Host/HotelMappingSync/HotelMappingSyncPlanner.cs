using Infrastructure.Redis.HotelMatchingRedis;

namespace Tests.Host.HotelMappingSync;

public static class HotelMappingSyncPlanner
{
    public static IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>> BuildDesiredMappings(
        IEnumerable<HotelMappingSourceRow> sourceRows)
    {
        ArgumentNullException.ThrowIfNull(sourceRows);

        var grouped = new Dictionary<string, List<SupHotelInfoDto>>(StringComparer.Ordinal);
        var assignedSupplierHotels = new Dictionary<string, string>(StringComparer.Ordinal);
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

            var supplierHotel = $"{row.SupId}_{supplierHotelCode}";
            if (!assignedSupplierHotels.TryAdd(supplierHotel, mtHotelCode))
            {
                throw new InvalidOperationException(
                    $"Duplicate hotel mapping field after normalization: {supplierHotel}.");
            }

            if (!grouped.TryGetValue(mtHotelCode, out var supplierHotels))
            {
                supplierHotels = [];
                grouped.Add(mtHotelCode, supplierHotels);
            }

            supplierHotels.Add(new SupHotelInfoDto
            {
                MTHotelCode = mtHotelCode,
                SupId = row.SupId,
                SContryCode = row.CountryCode?.Trim() ?? string.Empty,
                SCityCode = row.CityCode?.Trim() ?? string.Empty,
                SHotelCode = supplierHotelCode,
                SHotelName = FirstNotEmpty(row.HotelNameEn, row.HotelName),
                LV = 1
            });
        }

        return grouped.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<SupHotelInfoDto>)pair.Value
                .OrderBy(hotel => hotel.SupId)
                .ThenBy(hotel => hotel.SHotelCode, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    public static HotelMappingSyncPlan CreatePlan(
        IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>> desired,
        IReadOnlyDictionary<string, IReadOnlyList<SupHotelInfoDto>> current)
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
            else if (!HaveSameSupplierHotels(currentValue, pair.Value))
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

    static bool HaveSameSupplierHotels(
        IReadOnlyList<SupHotelInfoDto> left,
        IReadOnlyList<SupHotelInfoDto> right)
    {
        return left.OrderBy(Identity, StringComparer.Ordinal)
            .SequenceEqual(right.OrderBy(Identity, StringComparer.Ordinal));
    }

    static string Identity(SupHotelInfoDto hotel) => $"{hotel.SupId}_{hotel.SHotelCode}";

    static string FirstNotEmpty(string? preferred, string? fallback)
    {
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : fallback?.Trim() ?? string.Empty;
    }
}

using Infrastructure.Redis.HotelMatchingRedis;

namespace Tests.Host.HotelMappingSync;

public sealed record HotelMappingSourceRow(
    int SupId,
    string SHotelCode,
    string MTHotelCode,
    string? CountryCode = null,
    string? CityCode = null,
    string? HotelName = null,
    string? HotelNameEn = null);

public sealed record HotelMappingSyncPlan(
    IReadOnlyList<HotelMappingSyncOperation> Added,
    IReadOnlyList<HotelMappingSyncOperation> Updated,
    IReadOnlyList<HotelMappingSyncOperation> Deleted)
{
    public IReadOnlyList<HotelMappingSyncOperation> Operations =>
        [.. Added, .. Updated, .. Deleted];
}

public sealed record HotelMappingSyncProgress(
    string Phase = "starting",
    int SourceCount = 0,
    int RedisCount = 0,
    int AddedCount = 0,
    int UpdatedCount = 0,
    int DeletedCount = 0,
    int AppliedCount = 0,
    int QueuedCount = 0,
    int PendingCount = 0,
    string? Warning = null);

public sealed record HotelMappingRedisSyncStatus(
    string State,
    string Phase,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int SourceCount,
    int RedisCount,
    int AddedCount,
    int UpdatedCount,
    int DeletedCount,
    int AppliedCount,
    int QueuedCount,
    int PendingCount,
    string? Error,
    string? Warning = null)
{
    public static HotelMappingRedisSyncStatus Idle { get; } = new(
        "idle", "idle", null, null, 0, 0, 0, 0, 0, 0, 0, 0, null);
}

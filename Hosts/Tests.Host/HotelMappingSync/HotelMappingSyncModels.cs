namespace Tests.Host.HotelMappingSync;

public sealed record HotelMappingSourceRow(
    int SupId,
    string SHotelCode,
    string MTHotelCode);

public enum HotelMappingSyncOperationType
{
    Add,
    Update,
    Delete
}

public sealed record HotelMappingSyncOperation(
    HotelMappingSyncOperationType Type,
    string Field,
    string? OldValue,
    string? NewValue)
{
    public string QueueValue => Type == HotelMappingSyncOperationType.Delete
        ? OldValue!
        : NewValue!;
}

public sealed record HotelMappingOutboxEntry(
    string Id,
    HotelMappingSyncOperationType Operation,
    string Field,
    string? OldValue,
    string? NewValue,
    string QueueValue,
    DateTimeOffset CreatedAt);

public sealed record HotelMappingSyncPlan(
    IReadOnlyList<HotelMappingSyncOperation> Added,
    IReadOnlyList<HotelMappingSyncOperation> Updated,
    IReadOnlyList<HotelMappingSyncOperation> Deleted)
{
    public IReadOnlyList<HotelMappingSyncOperation> Operations =>
        [.. Added, .. Updated, .. Deleted];
}

public sealed record HotelMappingSyncProgress(
    int SourceCount = 0,
    int RedisCount = 0,
    int AddedCount = 0,
    int UpdatedCount = 0,
    int DeletedCount = 0,
    int QueuedCount = 0,
    int PendingCount = 0);

public sealed record HotelMappingRedisSyncStatus(
    string State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int SourceCount,
    int RedisCount,
    int AddedCount,
    int UpdatedCount,
    int DeletedCount,
    int QueuedCount,
    int PendingCount,
    string? Error)
{
    public static HotelMappingRedisSyncStatus Idle { get; } = new(
        "idle", null, null, 0, 0, 0, 0, 0, 0, 0, null);
}

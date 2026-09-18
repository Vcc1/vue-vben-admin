namespace HotelMatchingPipeline.App.Models;

public sealed record HotelMatchVetoRequest(
    int SupId,
    string SHotelCode,
    string MTHotelCode,
    string Operator,
    string Reason);

public sealed record HotelMatchVetoResult(
    bool Success,
    string Status,
    int SupId,
    string SHotelCode,
    string MTHotelCode,
    DateTimeOffset? DatabaseUpdatedAt,
    DateTimeOffset? RedisSyncedAt,
    string? CurrentMTHotelCode = null);

public enum HotelMatchVetoCreateStatus
{
    Created,
    AlreadyExists,
    SupplierNotFound,
    MatchNotFound,
    CurrentMatchChanged
}

public sealed record HotelMatchVetoCreateResult(
    HotelMatchVetoCreateStatus Status,
    long? VetoId,
    string? CurrentMTHotelCode = null);

public sealed record HotelMatchVetoSyncItem(
    long Id,
    int SupHotelId,
    int SupId,
    string SHotelCode,
    string MTHotelCode,
    string Operator,
    string Reason,
    int RedisSyncAttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset DatabaseUpdatedAt,
    DateTimeOffset? FirstRedisSyncFailureAt,
    DateTimeOffset? LastRedisSyncAttemptAt,
    string? LastRedisSyncError,
    DateTimeOffset? FailureAlertSentAt,
    DateTimeOffset? RedisSyncedAt,
    DateTimeOffset? RecoveryAlertSentAt);

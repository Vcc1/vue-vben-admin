using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelCleaningRepository
{
    Task<HotelCleaningWorkSet> LoadCleaningWorkSetAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        int pendingIdBatchSize,
        Action<HotelCleaningScanProgress> reportScanProgress,
        Func<int, CancellationToken, ValueTask> enqueuePendingId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RawHotelData>> GetHotelsByIdsAsync(
        HotelDataType type,
        IReadOnlyCollection<int> dataIds,
        CancellationToken cancellationToken);

    Task<int> SaveCleanedHotelsAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        CancellationToken cancellationToken);

    Task RecordCleaningFailuresAsync(
        IReadOnlyCollection<HotelCleaningFailureRecord> failures,
        CancellationToken cancellationToken);
}

public sealed record HotelCleaningWorkSet(
    long Total,
    long Cleaned,
    long Failed)
{
    public HotelCleaningProgress Progress => new(Total, Cleaned, Failed);
}

public sealed record HotelCleaningScanProgress(
    long Total,
    long Scanned,
    long Cleaned,
    long Failed,
    long Pending);

public sealed record HotelCleaningProgress(long Total, long Cleaned, long Failed)
{
    public long Remaining => Math.Max(0, Total - Cleaned);

    public long Pending => Math.Max(0, Remaining - Failed);
}

public sealed record HotelCleaningFailureRecord(
    HotelDataType SourceType,
    long DataId,
    int? SupplierId,
    int? SourceId,
    string HotelCode,
    string ExceptionType,
    string ExceptionMessage,
    string? StackTrace,
    int CleanVersion,
    int RegionBoundaryVersion);

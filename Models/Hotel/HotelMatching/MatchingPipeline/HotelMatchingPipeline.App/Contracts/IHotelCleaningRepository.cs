using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelCleaningRepository
{
    Task<HotelCleaningProgress> GetCleaningProgressAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RawHotelData>> GetHotelsToCleanAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        int batchSize,
        CancellationToken cancellationToken);

    Task<int> SaveCleanedHotelsAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        CancellationToken cancellationToken);

    Task RecordCleaningFailuresAsync(
        IReadOnlyCollection<HotelCleaningFailureRecord> failures,
        CancellationToken cancellationToken);
}

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

using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.App.Contracts;

public sealed record HotelMatchWorkItem(
    CleanedHotel Hotel,
    long SourceDataVersion,
    string NormalizedDataHash,
    int CleanVersion);

public interface IHotelMatchPipelineRepository
{
    Task LoadPendingSupplierHotelIdsAsync(
        Func<int, CancellationToken, ValueTask> enqueueId,
        CancellationToken cancellationToken);

    Task<HotelMatchingWorkItem> LoadMatchingWorkItemAsync(
        int supHotelId,
        int recallRadiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken);

    Task SaveMatchEvaluationsAsync(
        IReadOnlyCollection<HotelMatchEvaluation> evaluations,
        int matchingVersion,
        CancellationToken cancellationToken);
}

public interface IHotelMatchingRepository
{
    Task<IReadOnlyList<HotelMatchWorkItem>> ClaimUnmatchedSupplierHotelsAsync(
        int matchingVersion,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CleanedHotel>> RecallOtaCandidatesAsync(
        HotelMatchWorkItem supplier,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CleanedHotel>> RecallSupplierCandidatesAsync(
        CleanedHotel ota,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetTrustedAliasesAsync(
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken);

    Task CompleteMatchAsync(
        HotelMatchWorkItem workItem,
        HotelMatchDecision decision,
        int matchingVersion,
        string? error,
        DateTimeOffset? nextMatchTime,
        CancellationToken cancellationToken);

    Task<HotelMatchWorkItem?> GetSupplierHotelAsync(
        int supId,
        string supplierHotelCode,
        CancellationToken cancellationToken);

    Task<CleanedHotel?> GetOtaHotelAsync(string mtHotelCode, CancellationToken cancellationToken);

    Task<bool> IsVetoedAsync(
        int supId,
        string supplierHotelCode,
        string mtHotelCode,
        CancellationToken cancellationToken);
}

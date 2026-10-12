using HotelResearch.Contracts;

namespace OTARoomBuilder.Contracts;

public interface IAiRoomBuilder
{
    Task<IReadOnlyList<AiRoomDecision>> ProcessAsync(
        string mtHotelCode,
        IReadOnlyList<AiRoomCandidate> candidates,
        HotelFactsResult? facts,
        CancellationToken cancellationToken = default);
}

public interface IAiRoomAuditRepository
{
    Task<bool> ShouldProcessAsync(
        AiRoomCandidate candidate,
        int? searchResultVersion,
        CancellationToken cancellationToken = default);

    Task UpsertDecisionAsync(
        AiRoomCandidate candidate,
        AiRoomDecision decision,
        int? searchResultVersion,
        CancellationToken cancellationToken = default);

    Task RecordCreateFailureAsync(
        AiRoomCandidate candidate,
        int attempts,
        string errorMessage,
        int? searchResultVersion,
        CancellationToken cancellationToken = default);

    Task MarkCreateSucceededAsync(
        AiRoomCandidate candidate,
        CancellationToken cancellationToken = default);
}

public sealed record AiRoomCandidate
{
    public required int SupplierRoomId { get; init; }

    public required int SupId { get; init; }

    public required string SHotelCode { get; init; }

    public required string SRoomCode { get; init; }

    public required string MTHotelCode { get; init; }

    public string? RoomNameChinese { get; init; }

    public string? RoomNameEnglish { get; init; }

    public string? BedInfo { get; init; }

    public decimal? AreaMin { get; init; }

    public decimal? AreaMax { get; init; }

    public int? MaxOccupancy { get; init; }

    public int? SmokeType { get; init; }

    public IReadOnlyList<string> MatchedSearchRoomNames { get; init; } = [];
}

public sealed record AiRoomDecision
{
    public required int SupplierRoomId { get; init; }

    public required bool SuitableForCreation { get; init; }

    public string? RoomNameChinese { get; init; }

    public string? RoomNameEnglish { get; init; }

    public string? RoomNameLocal { get; init; }

    public string? Area { get; init; }

    public string? BedType { get; init; }

    public bool? IsNonSmoking { get; init; }

    public string? ViewEnglish { get; init; }

    public IReadOnlyList<string> Facilities { get; init; } = [];

    public IReadOnlyList<string> OffersAndServices { get; init; } = [];

    public int? MaxOccupancy { get; init; }

    public int RecommendedMaxOccupancy { get; init; } = 2;

    public required string OccupancySource { get; init; }

    public required string ReasonChinese { get; init; }
}

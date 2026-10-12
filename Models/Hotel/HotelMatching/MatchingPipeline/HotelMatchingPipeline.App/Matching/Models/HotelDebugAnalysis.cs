using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.App.Models;

public enum HotelDebugSourceType
{
    Ota = 1,
    Supplier = 2
}

public sealed record HotelDebugRequest(
    HotelDebugSourceType SourceType,
    string HotelCode,
    int? SupId = null);

public sealed record HotelDebugAnalysis(
    HotelDebugRequest Request,
    CleanedHotel? Source,
    HotelMatchDecision? Decision,
    IReadOnlyList<string> Errors)
{
    public IReadOnlyList<CleanedHotel> RecalledCandidates { get; init; } = [];
}

public sealed record HotelManualReviewRequest(
    int SupId,
    string SHotelCode,
    string? MTHotelCode,
    string? Reason);

public sealed record HotelManualReviewResult(bool Success, string Status, string? Error);

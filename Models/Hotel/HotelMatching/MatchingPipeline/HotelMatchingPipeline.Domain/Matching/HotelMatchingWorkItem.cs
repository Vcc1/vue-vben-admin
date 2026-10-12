using HotelMatchingPipeline.Domain.HotelNameMatching;

namespace HotelMatchingPipeline.Domain.Hotels;

public enum HotelMatchFactSource : byte
{
    Ota = 1,
    Supplier = 2,
    WebSearch = 3
}

public sealed record HotelMatchFact(
    HotelMatchFactSource Source,
    int HotelId,
    int? SupplierId,
    string HotelCode,
    string? MTHotelCode,
    string? HotelName,
    string? HotelNameEn,
    decimal? Longitude,
    decimal? Latitude,
    decimal? StarLevel,
    string? Address,
    IReadOnlyList<string> Phones,
    string? SourceSiteName = null,
    string? SourceUrl = null,
    int? SourceResultVersion = null);

public sealed record HotelMatchingWorkItem(
    int SupHotelId,
    HotelMatchFact PendingSupplier,
    IReadOnlyList<HotelMatchFact> NearbyOtaHotels,
    IReadOnlyList<HotelMatchFact> NearbySupplierHotels,
    IReadOnlyList<HotelMatchFact> OtaLinkedSupplierHotels)
{
    public IReadOnlySet<string> VetoedMTHotelCodes { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record HotelMatchEvidence(
    HotelMatchFactSource Source,
    int HotelId,
    string? PendingValue,
    string? EvidenceValue,
    double? DistanceMeters = null);

public sealed record HotelMatchedCandidate(
    string MTHotelCode,
    HotelMatchEvidence NameEvidence,
    HotelNameMatchKind NameMatchKind,
    HotelMatchEvidence CoordinateEvidence,
    HotelMatchEvidence? StarEvidence,
    HotelMatchEvidence? AddressEvidence,
    HotelMatchEvidence? PhoneEvidence,
    int OtaFactCount,
    int SupplierFactCount);

public sealed record HotelMatchEvaluation(
    int SupHotelId,
    int SupId,
    string SupHotelCode,
    IReadOnlyList<HotelMatchedCandidate> Matches)
{
    public string ResolvedMTHotelCode =>
        Matches.Count == 1 ? Matches[0].MTHotelCode : string.Empty;

    public bool HasConflict => Matches.Count > 1;
}

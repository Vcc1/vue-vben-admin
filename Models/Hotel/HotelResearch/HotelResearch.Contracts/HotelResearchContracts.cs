using AgentPlatform.Contracts;
using HotelBusiness.Contracts;
using System.Text.Json.Serialization;

namespace HotelResearch.Contracts;

public sealed record HotelResearchRequest
{
    public required string MTHotelCode { get; init; }

    public string? ConversationId { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<HotelResearchResolveMode>))]
public enum HotelResearchResolveMode
{
    RefreshAlways,
    ReuseOrRefresh,
    ReuseOnly
}

public sealed record HotelResearchResolveRequest
{
    public required string MTHotelCode { get; init; }

    public HotelResearchResolveMode Mode { get; init; } = HotelResearchResolveMode.ReuseOrRefresh;
}

public sealed record HotelResearchResult
{
    public int FileSchemaVersion { get; init; } = 1;

    public string OperationType { get; init; } = HotelBusinessOperations.HotelResearch;

    public required string MTHotelCode { get; init; }

    public required string RunId { get; init; }

    public required int ResultVersion { get; init; }

    public required string ConversationId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset ResearchedAt { get; init; }

    public required HotelProcessorDescriptor Processor { get; init; }

    public required HotelResearchDatabaseHotel DatabaseHotel { get; init; }

    public IReadOnlyList<HotelResearchSite> Sites { get; init; } = [];

    public IReadOnlyList<HotelResearchRejectedCandidate> RejectedCandidates { get; init; } = [];

    public IReadOnlyList<string> SearchQueries { get; init; } = [];

    public IReadOnlyList<AgentSource> Sources { get; init; } = [];

    public AgentUsage? Usage { get; init; }
}

public sealed record HotelResearchDatabaseHotel
{
    public required int HotelId { get; init; }

    public required string MTHotelCode { get; init; }

    public required string HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public string? Address { get; init; }

    public string? AddressEnglish { get; init; }

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public string? CountryNameEnglish { get; init; }

    public string? CityName { get; init; }

    public string? CityNameEnglish { get; init; }

    public decimal? Longitude { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? StarRating { get; init; }

    public string? OfficialWebsite { get; init; }

    public IReadOnlyList<string> HotelPhones { get; init; } = [];

    public IReadOnlyList<HotelResearchDatabaseRoom> Rooms { get; init; } = [];
}

public sealed record HotelResearchDatabaseRoom
{
    public required int RoomId { get; init; }

    public string? RoomNameChinese { get; init; }

    public string? RoomNameEnglish { get; init; }

    public string? Area { get; init; }

    public string? BedType { get; init; }

    public bool? IsNonSmoking { get; init; }

    public string? View { get; init; }
}

public sealed record HotelResearchSite
{
    public required string SiteName { get; init; }

    public required string SiteUrl { get; init; }

    public IReadOnlyList<string> SourceUrls { get; init; } = [];

    public string? HotelNameChinese { get; init; }

    public string? HotelNameEnglish { get; init; }

    public string? HotelNameLocal { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public decimal? Longitude { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? StarRating { get; init; }

    public string? HotelPhone { get; init; }

    public string? AddressChinese { get; init; }

    public string? AddressEnglish { get; init; }

    public string? AddressLocal { get; init; }

    public IReadOnlyList<HotelResearchRoom> Rooms { get; init; } = [];

    public HotelResearchIdentityVerification? IdentityVerification { get; init; }
}

public sealed record HotelResearchRejectedCandidate
{
    public required string SiteName { get; init; }

    public required string SiteUrl { get; init; }

    public IReadOnlyList<string> CandidateNames { get; init; } = [];

    public required HotelResearchIdentityVerification IdentityVerification { get; init; }
}

public sealed record HotelResearchIdentityVerification
{
    public required string Status { get; init; }

    public string? MatchedSeedName { get; init; }

    public string? MatchedCandidateName { get; init; }

    public string? NameMatchKind { get; init; }

    public IReadOnlyList<string> MatchedSignals { get; init; } = [];

    public IReadOnlyList<string> Conflicts { get; init; } = [];

    public decimal? CoordinateDistanceMeters { get; init; }

    public required string Summary { get; init; }
}

public sealed record HotelResearchRoom
{
    public string? RoomNameChinese { get; init; }

    public string? RoomNameEnglish { get; init; }

    public string? RoomNameLocal { get; init; }

    public string? Area { get; init; }

    public string? BedType { get; init; }

    public bool? IsNonSmoking { get; init; }

    public string? ViewEnglish { get; init; }

    public IReadOnlyList<string> Facilities { get; init; } = [];

    public IReadOnlyList<string> OffersAndServices { get; init; } = [];
}

public sealed record HotelFactsResult
{
    public int FileSchemaVersion { get; init; } = 1;

    public int FactSchemaVersion { get; init; } = 1;

    public required string MTHotelCode { get; init; }

    public required int SourceResultVersion { get; init; }

    public required string SourceRunId { get; init; }

    public required DateTimeOffset ResearchedAt { get; init; }

    public required HotelProcessorDescriptor Processor { get; init; }

    public required HotelResearchDatabaseHotel DatabaseHotel { get; init; }

    public IReadOnlyList<HotelResearchSite> ConfirmedSites { get; init; } = [];

    public IReadOnlyList<AgentSource> Sources { get; init; } = [];
}

public sealed record HotelFactsBatchRequest
{
    public IReadOnlyList<string> MTHotelCodes { get; init; } = [];

    public HotelResearchResolveMode Mode { get; init; } = HotelResearchResolveMode.ReuseOrRefresh;
}

public sealed record HotelFactsBatchItem
{
    public required string MTHotelCode { get; init; }

    public HotelFactsResult? Facts { get; init; }

    public bool NotFound { get; init; }
}

public sealed record HotelBusinessDataSnapshot
{
    public required string MTHotelCode { get; init; }

    public HotelBusinessDatabaseHotelRecord? DatabaseHotel { get; init; }

    public IReadOnlyList<HotelBusinessDataRecord> OriginalMatches { get; init; } = [];

    public IReadOnlyList<HotelBusinessDataRecord> NewMatches { get; init; } = [];
}

public sealed record HotelBusinessDatabaseHotelRecord
{
    public required int HotelId { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public string? CountryCode { get; init; }

    public IReadOnlyList<string> Phones { get; init; } = [];

    public IReadOnlyList<HotelBusinessPhoneValue> PhoneDetails { get; init; } = [];

    public string? Address { get; init; }

    public string? AddressEnglish { get; init; }

    public decimal? Longitude { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? StarRating { get; init; }

    public DateTime UpdatedAt { get; init; }
}

public sealed record HotelBusinessDataRecord
{
    public required int SupHotelId { get; init; }

    public required int SupId { get; init; }

    public required string SHotelCode { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEnglish { get; init; }

    public string? CountryCode { get; init; }

    public IReadOnlyList<string> Phones { get; init; } = [];

    public IReadOnlyList<HotelBusinessPhoneValue> PhoneDetails { get; init; } = [];

    public string? Address { get; init; }

    public decimal? Longitude { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? StarRating { get; init; }

    public DateTime UpdatedAt { get; init; }

    public HotelBusinessMatchEvidence? MatchEvidence { get; init; }
}

public sealed record HotelBusinessPhoneValue
{
    public required string Raw { get; init; }

    public required string Value { get; init; }

    public bool IsNormalized { get; init; }
}

public sealed record HotelPhoneNormalizationBatchRequest
{
    public IReadOnlyList<HotelPhoneNormalizationRequestItem> Items { get; init; } = [];
}

public sealed record HotelPhoneNormalizationRequestItem
{
    public required string Id { get; init; }

    public required string Raw { get; init; }

    public string? CountryCode { get; init; }
}

public sealed record HotelPhoneNormalizationBatchResult
{
    public IReadOnlyList<HotelPhoneNormalizationResultItem> Items { get; init; } = [];
}

public sealed record HotelPhoneNormalizationResultItem
{
    public required string Id { get; init; }

    public required string Input { get; init; }

    public IReadOnlyList<HotelPhoneNormalizationValue> Values { get; init; } = [];
}

public sealed record HotelPhoneNormalizationValue
{
    public required string Raw { get; init; }

    public string? E164 { get; init; }

    public string? NationalNumber { get; init; }

    public string? CountryCallingCode { get; init; }

    public string? AreaCode { get; init; }

    public string? Extension { get; init; }

    public string? RegionCode { get; init; }

    public string? NumberType { get; init; }

    public bool IsValid { get; init; }

    public string? Error { get; init; }
}

public sealed record HotelBusinessMatchEvidence
{
    public required long MatchResultId { get; init; }

    public string? NamePendingValue { get; init; }

    public string? NameEvidenceValue { get; init; }

    public string? AddressPendingValue { get; init; }

    public string? AddressEvidenceValue { get; init; }

    public string? PhonePendingValue { get; init; }

    public string? PhoneEvidenceValue { get; init; }

    public decimal DistanceMeters { get; init; }

    public DateTime MatchedAt { get; init; }
}

public interface IHotelResearchService
{
    Task<HotelResearchResult?> ResearchAsync(
        HotelResearchRequest request,
        CancellationToken cancellationToken = default);

    Task<HotelResearchResult?> ResolveAsync(
        HotelResearchResolveRequest request,
        CancellationToken cancellationToken = default);

    Task<HotelFactsResult?> GetFactsAsync(
        string mtHotelCode,
        int? resultVersion = null,
        CancellationToken cancellationToken = default);
}

public interface IHotelFactsClient
{
    Task<HotelFactsResult?> ResolveAsync(
        string mtHotelCode,
        HotelResearchResolveMode mode = HotelResearchResolveMode.ReuseOrRefresh,
        CancellationToken cancellationToken = default);

    Task<HotelFactsResult?> GetAsync(
        string mtHotelCode,
        int? resultVersion = null,
        CancellationToken cancellationToken = default);
}

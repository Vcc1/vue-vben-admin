namespace HotelMatchingPipeline.Domain.Hotels;

public sealed record RegionResolution(
    string? Level1Code,
    string? Level1Name,
    string? Level2Code,
    string? Level2Name,
    string? Level3Code,
    string? Level3Name,
    RegionResolveStatus Status,
    int BoundaryVersion)
{
    public string? TelephoneAreaCode { get; init; }

    public bool IsResolved => Status == RegionResolveStatus.Resolved;

    public static RegionResolution NotFound(int boundaryVersion = 0)
        => new(null, null, null, null, null, null, RegionResolveStatus.NotFound, boundaryVersion);
}

public enum RegionResolveStatus
{
    Unknown = 0,
    Resolved = 1,
    NotFound = 2,
    BoundaryAmbiguous = 3,
    InvalidCoordinate = 4
}

public interface IHotelRegionResolver
{
    RegionResolution Resolve(decimal longitude, decimal latitude, CancellationToken cancellationToken = default);

    RegionResolution Resolve(
        decimal longitude,
        decimal latitude,
        string? preferredCountryCode,
        CancellationToken cancellationToken = default)
        => Resolve(longitude, latitude, cancellationToken);
}

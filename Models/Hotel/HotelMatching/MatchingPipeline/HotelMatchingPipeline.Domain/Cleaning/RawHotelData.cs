namespace HotelMatchingPipeline.Domain.Hotels;

public sealed class RawHotelData
{
    public RawHotelData(HotelDataType type, string hotelCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hotelCode);

        Type = type;
        HotelCode = hotelCode;
    }

    public HotelDataType Type { get; }

    public long? Id { get; init; }

    public int? PlatformHotelId { get; init; }

    public int? SupplierId { get; init; }

    public int? SourceSystemId { get; init; }

    public string HotelCode { get; }

    public string? MtHotelCode { get; init; }

    public int? State { get; init; }

    public int? SaleTopLevel { get; init; }

    public int? IsHandMatch { get; init; }

    public int? ReMatchState { get; init; }

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public string? CityCode { get; init; }

    public string? CityName { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEn { get; init; }

    public string? EnAddress { get; init; }

    public int? Estate { get; init; }

    public int? Flag { get; init; }

    public int? AreaId { get; init; }

    public int? CountryId { get; init; }

    public int? ProvinceId { get; init; }

    public int? RootCityId { get; init; }

    public int? CityId { get; init; }

    public string? CountryNameEn { get; init; }

    public string? CityNameEn { get; init; }

    public string? WebSite { get; init; }

    public string? RawStarLevel { get; init; }

    public decimal? StarLevel { get; init; }

    public string? Telephone { get; init; }

    public string? Fax { get; init; }

    public int? GpsType { get; init; }

    public decimal? Longitude { get; init; }

    public decimal? Latitude { get; init; }

    public string? Address { get; init; }

    public string? PostCode { get; init; }

    public string? Email { get; init; }

    public string? RawPayload { get; init; }

    public string? CreatedBy { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public string? UpdatedBy { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public long SourceDataVersion { get; init; }

    public string? SourceDataHash { get; init; }
}

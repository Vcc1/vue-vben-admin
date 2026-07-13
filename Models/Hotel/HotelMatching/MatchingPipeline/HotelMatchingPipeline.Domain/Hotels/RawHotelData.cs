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

    public int? Id { get; init; }

    public int? SupplierId { get; init; }

    public string HotelCode { get; }

    public string? MtHotelCode { get; init; }

    public int? State { get; init; }

    public int? SaleTopLevel { get; init; }

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public string? CityCode { get; init; }

    public string? CityName { get; init; }

    public string? HotelName { get; init; }

    public string? HotelNameEn { get; init; }

    public int? StarLevel { get; init; }

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
}

namespace HotelMatchingPipeline.Domain.Hotels;

public sealed class CleanedHotel
{
    public CleanedHotel(
        HotelDataType type,
        string hotelCode,
        string hotelName,
        int? supplierId = null,
        string? mtHotelCode = null,
        string? countryCode = null,
        string? countryName = null,
        string? cityCode = null,
        string? cityName = null,
        string? hotelNameEn = null,
        string? address = null,
        string? postCode = null,
        string? telephone = null,
        string? fax = null,
        string? email = null,
        int? starLevel = null,
        int? gpsType = null,
        decimal? longitude = null,
        decimal? latitude = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hotelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotelName);

        Type = type;
        HotelCode = hotelCode;
        HotelName = hotelName;
        SupplierId = supplierId;
        MtHotelCode = mtHotelCode;
        CountryCode = countryCode;
        CountryName = countryName;
        CityCode = cityCode;
        CityName = cityName;
        HotelNameEn = hotelNameEn;
        Address = address;
        PostCode = postCode;
        Telephone = telephone;
        Fax = fax;
        Email = email;
        StarLevel = starLevel;
        GpsType = gpsType;
        Longitude = longitude;
        Latitude = latitude;
    }

    public HotelDataType Type { get; }

    public int? SupplierId { get; }

    public string HotelCode { get; }

    public string? MtHotelCode { get; }

    public string HotelName { get; }

    public string? HotelNameEn { get; }

    public string? CountryCode { get; }

    public string? CountryName { get; }

    public string? CityCode { get; }

    public string? CityName { get; }

    public string? Address { get; }

    public string? PostCode { get; }

    public string? Telephone { get; }

    public string? Fax { get; }

    public string? Email { get; }

    public int? StarLevel { get; }

    public int? GpsType { get; }

    public decimal? Longitude { get; }

    public decimal? Latitude { get; }
}

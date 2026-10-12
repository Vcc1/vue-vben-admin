namespace Infrastructure.HTTP.OTAHotelInfo.Models;

public sealed class BaseHotelInfo
{
    public int HotelID { get; set; }

    public string? HotelCode { get; set; }

    public string? HotelName { get; set; }

    public string? HotelEnName { get; set; }

    public int CityID { get; set; }

    public string? CityCode { get; set; }

    public string? CityName { get; set; }

    public string? CityNameEn { get; set; }

    public int ProvinceID { get; set; }

    public string? ProvinceName { get; set; }

    public int CountryID { get; set; }

    public string? CountryName { get; set; }

    public string? CountryNameEn { get; set; }

    public string? Address { get; set; }

    public string? EnAddress { get; set; }

    public string? Tel { get; set; }

    public string? Email { get; set; }

    public string? Fax { get; set; }

    public string? PostCode { get; set; }

    public string? StarLevel { get; set; }

    public string? Longitude { get; set; }

    public string? Latitude { get; set; }

    /// <summary>
    /// 1 when online; -1 when offline.
    /// </summary>
    public int Estate { get; set; }

    public string? CountryCode { get; set; }

    public string? WebSite { get; set; }

    public int GpsType { get; set; }

    public DateTime? LastModifyTime { get; set; }
}

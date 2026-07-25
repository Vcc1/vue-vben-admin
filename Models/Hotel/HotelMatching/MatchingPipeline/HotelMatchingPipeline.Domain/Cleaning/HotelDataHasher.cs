using System.Security.Cryptography;
using System.Text;

namespace HotelMatchingPipeline.Domain.Hotels;

public static class HotelDataHasher
{
    public static string HashRaw(RawHotelData raw)
        => Hash(
            raw.Type,
            raw.SupplierId,
            raw.HotelCode,
            raw.MtHotelCode,
            raw.CountryCode,
            raw.CityCode,
            raw.HotelName,
            raw.HotelNameEn,
            raw.RawStarLevel,
            raw.StarLevel,
            raw.Telephone,
            raw.Fax,
            raw.GpsType,
            raw.Longitude,
            raw.Latitude,
            raw.Address,
            raw.PostCode,
            raw.Email);

    public static string HashCleaned(CleanedHotel hotel)
        => Hash(
            hotel.Type,
            hotel.SupplierId,
            hotel.HotelCode,
            hotel.MtHotelCode,
            hotel.CountryCode,
            hotel.CityCode,
            hotel.HotelName,
            hotel.HotelNameEn,
            hotel.StarLevel,
            hotel.Telephone,
            hotel.Longitude,
            hotel.Latitude,
            hotel.RegionLevel1Code,
            hotel.RegionLevel2Code,
            hotel.RegionLevel3Code,
            string.Join(',', hotel.NormalizedPhoneDetails.Select(phone => string.Join('|',
                phone.E164,
                phone.NationalNumber,
                phone.CountryCallingCode,
                phone.RegionCode,
                phone.NumberType,
                phone.AreaCode,
                phone.Extension,
                phone.IsValid,
                phone.Error))),
            hotel.DataQualityStatus,
            hotel.DataQualityFlags);

    static string Hash(params object?[] values)
    {
        var text = string.Join('\u001F', values.Select(value => value?.ToString() ?? string.Empty));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}

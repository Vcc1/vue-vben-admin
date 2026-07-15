using Infrastructure.HTTP.OTAHotelInfo;
using Infrastructure.HTTP.OTAHotelInfo.Models;
using Microsoft.Extensions.Logging;
using OTAHotelInfoSync.Contracts;
using OTAHotelInfoSync.Models;

namespace Infrastructure.OTAHotelInfoSync;

public sealed class OtaHotelInfoSource : IOTAHotelInfoSource
{
    readonly IOtaHotelInfoClient _client;
    readonly ILogger<OtaHotelInfoSource> _logger;

    public OtaHotelInfoSource(
        IOtaHotelInfoClient client,
        ILogger<OtaHotelInfoSource> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OTAHotelInfoRecord>> QueryByMinHotelIdAsync(
        int minHotelId,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _client.QueryByMinHotelIdAsync(minHotelId, pageSize, cancellationToken);
        _logger.LogDebug(
            "Loaded OTA hotel batch by ID. MinHotelId={MinHotelId}, PageSize={PageSize}, Count={Count}",
            minHotelId,
            pageSize,
            result.Items.Count);
        return result.Items.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<OTAHotelInfoRecord>> QueryByTimeAsync(
        DateTime startTime,
        DateTime? endTime,
        CancellationToken cancellationToken)
    {
        var result = await _client.QueryByTimeAsync(startTime, endTime, cancellationToken);
        _logger.LogDebug(
            "Loaded OTA hotel batch by time. StartTime={StartTime}, EndTime={EndTime}, Count={Count}",
            startTime,
            endTime,
            result.Items.Count);
        return result.Items.Select(Map).ToArray();
    }

    static OTAHotelInfoRecord Map(BaseHotelInfo source)
    {
        return new OTAHotelInfoRecord
        {
            HotelID = source.HotelID,
            HotelCode = source.HotelCode,
            HotelName = source.HotelName,
            HotelEnName = source.HotelEnName,
            CityID = source.CityID,
            CityCode = source.CityCode,
            CityName = source.CityName,
            CityNameEn = source.CityNameEn,
            ProvinceID = source.ProvinceID,
            ProvinceName = source.ProvinceName,
            CountryID = source.CountryID,
            CountryName = source.CountryName,
            CountryNameEn = source.CountryNameEn,
            Address = source.Address,
            EnAddress = source.EnAddress,
            Tel = source.Tel,
            Email = source.Email,
            Fax = source.Fax,
            PostCode = source.PostCode,
            StarLevel = source.StarLevel,
            Longitude = source.Longitude,
            Latitude = source.Latitude,
            Estate = source.Estate,
            CountryCode = source.CountryCode,
            WebSite = source.WebSite,
            GpsType = source.GpsType,
            LastModifyTime = source.LastModifyTime
        };
    }
}

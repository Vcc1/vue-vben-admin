using Microsoft.Extensions.Logging;
using RoomMatch.App.Models;

namespace RoomMatch.App;

public sealed class OTAHotelRoomApp
{
    private readonly IRoomMappingQuery _query;
    private readonly IRoomMappingCache _cache;
    private readonly ILogger<OTAHotelRoomApp> _logger;

    public OTAHotelRoomApp(
        IRoomMappingQuery query,
        IRoomMappingCache cache,
        ILogger<OTAHotelRoomApp> logger)
    {
        _query = query;
        _cache = cache;
        _logger = logger;
    }

    public async Task<HotelRoomMappingInfoV2> GetHotelRoomMappingV2(
        string mtHotelCode,
        bool isShowWord,
        bool isOnlyOtaRooms,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var otaRoomInfos = await _query.GetOTARoomInfosAsync(mtHotelCode, cancellationToken);

            if (isOnlyOtaRooms)
            {
                return new HotelRoomMappingInfoV2(otaRoomInfos, null, null);
            }

            var supRoomInfos = await _query.GetSupRoomInfosAsync(mtHotelCode, cancellationToken);
            var roomMaps = await _query.GetRoomMapsAsync(mtHotelCode, cancellationToken);

            return new HotelRoomMappingInfoV2(otaRoomInfos, supRoomInfos, roomMaps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHotelRoomMappingV2 failed for {MTHotelCode}", mtHotelCode);
            throw;
        }
    }

    public async Task<object?> GetHotelRoomMappingCache(
        string mtHotelCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cache.GetCacheAsync(mtHotelCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHotelRoomMappingCache failed for {MTHotelCode}", mtHotelCode);
            throw;
        }
    }

    public async Task<List<WeeklyAgentStatDto>> GetWeeklyAgentStats(
        DateTime? startDate = null,
        DateTime? endDate = null,
        IEnumerable<int>? supIds = null,
        CancellationToken cancellationToken = default)
    {
        var end = (endDate ?? DateTime.UtcNow.Date).Date;
        var start = (startDate ?? end.AddDays(-6)).Date;
        var supIdArray = supIds?.ToArray();

        return await _query.GetWeeklyAgentStatsAsync(start, end, supIdArray, cancellationToken);
    }
}

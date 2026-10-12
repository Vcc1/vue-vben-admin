using RoomMatch.App.Models;

namespace RoomMatch.App;

public interface IRoomMappingQuery
{
    Task<List<OTARoomInfoDto>> GetOTARoomInfosAsync(string mtHotelCode, CancellationToken cancellationToken = default);
    Task<List<SupRoomInfoDto>> GetSupRoomInfosAsync(string mtHotelCode, CancellationToken cancellationToken = default);
    Task<List<RoomMapDto>> GetRoomMapsAsync(string mtHotelCode, CancellationToken cancellationToken = default);
    Task<List<WeeklyAgentStatDto>> GetWeeklyAgentStatsAsync(DateTime start, DateTime end, int[]? supIds, CancellationToken cancellationToken = default);
}

public interface IRoomMappingCache
{
    Task<object?> GetCacheAsync(string mtHotelCode);
}

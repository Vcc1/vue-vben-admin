using Infrastructures.RoomMatchingRedis;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoomMatch.App;
using RoomMatch.App.Models;

namespace RoomMatch.Infra;

public sealed class RoomMappingQueryRepository : IRoomMappingQuery
{
    private readonly RoomMappingDbContext _db;
    private readonly ILogger<RoomMappingQueryRepository> _logger;

    public RoomMappingQueryRepository(RoomMappingDbContext db, ILogger<RoomMappingQueryRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<OTARoomInfoDto>> GetOTARoomInfosAsync(string mtHotelCode, CancellationToken cancellationToken = default)
    {
        return await _db.OTARoomInfos
            .Where(e => e.MTHotelCode == mtHotelCode)
            .Select(e => new OTARoomInfoDto(
                e.Id, e.MTHotelCode, e.RoomNameCN, e.RoomNameEN, e.Person,
                e.RoomQuantity, e.FloorMin, e.FloorMax, e.AreaMin, e.AreaMax,
                e.Smoke, e.CreateTime, e.Status, e.Author))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SupRoomInfoDto>> GetSupRoomInfosAsync(string mtHotelCode, CancellationToken cancellationToken = default)
    {
        return await _db.SupRoomInfos
            .Where(e => e.MTHotelCode == mtHotelCode)
            .Select(e => new SupRoomInfoDto(
                e.Id, e.SHotelCode, e.SupId, e.SRoomNameCN, e.BedInfoKey,
                e.SRoomCode, e.MTHotelCode, e.SRoomNameEN, e.LastUpdateDate,
                e.BedInfo, e.Status, e.AcreageFrom, e.AcreageTo,
                e.SmokeType, e.MaxAdt, e.MaxChd))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RoomMapDto>> GetRoomMapsAsync(string mtHotelCode, CancellationToken cancellationToken = default)
    {
        return await _db.RoomMaps
            .Where(e => e.MTHotelCode == mtHotelCode)
            .Select(e => new RoomMapDto(
                e.Id, e.MTHotelCode, e.SupRoomId, e.OTARoomId,
                e.Status, e.CreateBy, e.CreateDate, e.MatchType))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WeeklyAgentStatDto>> GetWeeklyAgentStatsAsync(
        DateTime start, DateTime end, int[]? supIds, CancellationToken cancellationToken = default)
    {
        var baseQuery = _db.SupRoomInfoStats
            .Where(x => x.StatDate >= start && x.StatDate <= end);

        if (supIds is not null && supIds.Length > 0)
        {
            baseQuery = baseQuery.Where(x => supIds.Contains(x.SupId));
        }

        var latestPerDayQuery =
            from s in baseQuery
            group s by new { s.StatDate, s.SupId } into g
            select new
            {
                g.Key.StatDate,
                g.Key.SupId,
                MaxInsertTime = g.Max(z => z.InsertTime)
            };

        var query =
            from s in baseQuery
            join l in latestPerDayQuery
                on new { s.StatDate, s.SupId, s.InsertTime }
                equals new { l.StatDate, l.SupId, InsertTime = l.MaxInsertTime }
            select new WeeklyAgentStatDto(
                s.StatDate, s.SupId, s.BatchId, s.InsertTime,
                s.TotalCount, s.WithMTHotelCodeCount, s.WithRoomMapCount, s.UnmatchedRecentCount);

        var list = await query.ToListAsync(cancellationToken);
        return list
            .OrderBy(x => x.StatDate)
            .ThenBy(x => x.SupId)
            .ToList();
    }
}

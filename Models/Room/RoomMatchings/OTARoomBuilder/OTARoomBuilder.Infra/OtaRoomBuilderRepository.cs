using EFCore.BulkExtensions;
using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OTARoomBuilder.Contracts;
using System.Data;

namespace Infrastructure.OTARoomBuilder;

public sealed class OtaRoomBuilderRepository : IOtaRoomBuilderRepository
{
    readonly RoomMappingDbContext _dbContext;

    public OtaRoomBuilderRepository(RoomMappingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OtaRoomBuilderIncrementalBatch> LoadIncrementalMtHotelCodesAsync(
        OtaRoomBuilderIncrementalCursor? cursor,
        int batchSize,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var cursorDate = cursor?.LastUpdateDate ?? DateTime.MinValue;
        var cursorId = cursor?.LastId ?? 0;
        var uniqueHotelTarget = Math.Max(1, batchSize);
        var take = Math.Max(uniqueHotelTarget, uniqueHotelTarget * 5);
        var sql = """
    SELECT TOP (@Take)
        a.MTHotelCode,
        a.LastUpdate_Date AS LastUpdateDate,
        a.Id
    FROM dbo.SupRoomInfo a
    WHERE a.SupId IN (51, 32, 1, 75)
      AND a.MTHotelCode IS NOT NULL
      AND LTRIM(RTRIM(a.MTHotelCode)) <> ''
      AND
      (
          a.LastUpdate_Date > @CursorDate
          OR
          (
              a.LastUpdate_Date = @CursorDate
              AND a.Id > @CursorId
          )
      )
    ORDER BY a.LastUpdate_Date ASC, a.Id ASC;
    """;

        var parameters = new[]
        {   new SqlParameter("@Take", SqlDbType.Int){Value = take},
            new SqlParameter("@CursorDate", SqlDbType.DateTime2){ Value = cursorDate},
            new SqlParameter("@CursorId", SqlDbType.BigInt){ Value = cursorId}};

        var items = await _dbContext.Database
            .SqlQueryRaw<IncrementalHotelRow>(sql, parameters)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return new OtaRoomBuilderIncrementalBatch(Array.Empty<string>(), cursor);
        }

        var hotelCodes = items
            .Select(item => item.MTHotelCode)
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(uniqueHotelTarget)
            .ToArray();

        var lastItem = items[^1];
        return new OtaRoomBuilderIncrementalBatch(
            hotelCodes,
            new OtaRoomBuilderIncrementalCursor(lastItem.LastUpdateDate, lastItem.Id));
    }

    public async Task<IReadOnlyList<string>> LoadMtHotelCodesAsync(
        int lastDaysOffset,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var sql = $"""
            SELECT DISTINCT a.MTHotelCode
            FROM SupRoomInfo a
            WHERE a.SupId IN (51,32,1,75)
              AND a.LastUpdate_Date >= DATEADD(DAY, {lastDaysOffset}, GETDATE())
              AND a.MTHotelCode IS NOT NULL
              AND LTRIM(RTRIM(a.MTHotelCode)) <> ''
            ORDER BY a.MTHotelCode;
            """;

        return await _dbContext.Database
            .SqlQueryRaw<string>(sql)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupRoomInfo>> LoadPreferredSupplierRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        int? lastDaysOffset,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var hotelCodeList = string.Join(",", hotelCodes.Select(static code => $"'{code.Replace("'", "''")}'"));
        var lastDaysFilter = lastDaysOffset.HasValue
            ? $" AND a.LastUpdate_Date >= DATEADD(DAY, {lastDaysOffset.Value}, GETDATE())"
            : string.Empty;
        var sql = $"""
            SELECT a.*
            FROM SupRoomInfo a
            WHERE a.MTHotelCode IN ({hotelCodeList})
              AND a.SupId IN (51,32,1,75)
              {lastDaysFilter};
            """;

        return await _dbContext.SupRoomInfos.FromSqlRaw(sql).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OTARoomInfo>> LoadOtaRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        CancellationToken cancellationToken)
    {
        return await _dbContext.OTARoomInfos
            .Where(room => hotelCodes.Contains(room.MTHotelCode))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomMap>> LoadRoomMapsAsync(
        IReadOnlyCollection<string> hotelCodes,
        CancellationToken cancellationToken)
    {
        return await _dbContext.RoomMaps
            .Where(room => hotelCodes.Contains(room.MTHotelCode))
            .ToListAsync(cancellationToken);
    }

    public Task BulkInsertOtaRoomInfoPresAsync(
        IReadOnlyList<OTARoomInfoPre> rooms,
        CancellationToken cancellationToken)
    {
        return _dbContext.BulkInsertAsync(rooms.ToList(), cancellationToken: cancellationToken);
    }

    sealed class IncrementalHotelRow
    {
        public string MTHotelCode { get; set; } = string.Empty;

        public DateTime LastUpdateDate { get; set; }

        public int Id { get; set; }
    }
}

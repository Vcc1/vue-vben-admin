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
        IReadOnlyCollection<int> enabledSupIds,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var cursorDate = cursor?.LastUpdateDate ?? DateTime.MinValue;
        var cursorId = cursor?.LastId ?? 0;
        var uniqueHotelTarget = Math.Max(1, batchSize);
        var take = Math.Max(uniqueHotelTarget, uniqueHotelTarget * 5);
        var supIdFilter = BuildSupIdFilter(enabledSupIds);
        var sql = """
    SELECT TOP (@Take)
        a.MTHotelCode,
        a.LastUpdate_Date AS LastUpdateDate,
        a.Id
    FROM dbo.SupRoomInfo AS a WITH (NOLOCK)
    WHERE a.SupId IN ({0})
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
        sql = string.Format(sql, supIdFilter);

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
        IReadOnlyCollection<int> enabledSupIds,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var supIdFilter = BuildSupIdFilter(enabledSupIds);
        var sql = $"""
            SELECT DISTINCT a.MTHotelCode
            FROM dbo.SupRoomInfo AS a WITH (NOLOCK)
            WHERE a.SupId IN ({supIdFilter})
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
        IReadOnlyCollection<int> enabledSupIds,
        int? lastDaysOffset,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var hotelCodeParameter = CreateHotelCodesParameter(hotelCodes);
        if (hotelCodeParameter is null)
        {
            return [];
        }

        var supIdFilter = BuildSupIdFilter(enabledSupIds);
        return await LoadSupplierRoomsCoreAsync(
            hotelCodeParameter,
            $" AND a.SupId IN ({supIdFilter})",
            lastDaysOffset,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SupRoomInfo>> LoadAiSupplierRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        int? lastDaysOffset,
        CancellationToken cancellationToken)
    {
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));
        var hotelCodeParameter = CreateHotelCodesParameter(hotelCodes);
        if (hotelCodeParameter is null)
        {
            return [];
        }

        return await LoadSupplierRoomsCoreAsync(
            hotelCodeParameter,
            supplierFilter: string.Empty,
            lastDaysOffset,
            cancellationToken);
    }

    async Task<IReadOnlyList<SupRoomInfo>> LoadSupplierRoomsCoreAsync(
        SqlParameter hotelCodeParameter,
        string supplierFilter,
        int? lastDaysOffset,
        CancellationToken cancellationToken)
    {
        var sql = BuildSupplierRoomsQuery(supplierFilter, lastDaysOffset);

        var rows = await _dbContext.Database
            .SqlQueryRaw<SupplierRoomRow>(sql, hotelCodeParameter)
            .ToListAsync(cancellationToken);

        return rows.Select(MapSupplierRoom).ToList();
    }

    internal static string BuildSupplierRoomsQuery(string supplierFilter, int? lastDaysOffset)
    {
        var lastDaysFilter = lastDaysOffset.HasValue
            ? $" AND a.LastUpdate_Date >= DATEADD(DAY, {lastDaysOffset.Value}, GETDATE())"
            : string.Empty;

        return $"""
            SELECT
                a.Id,
                a.SupId,
                a.SHotelCode,
                a.SRoomCode,
                a.MTHotelCode,
                a.SRoomName_CN AS SRoomNameCN,
                a.SRoomName_EN AS SRoomNameEN,
                a.BedInfo,
                a.AcreageFrom,
                a.AcreageTo,
                a.SmokeType,
                a.MaxAdt
            FROM dbo.SupRoomInfo AS a WITH (NOLOCK)
            WHERE a.MTHotelCode IN (SELECT Value FROM @MTHotelCodes)
              {supplierFilter}
              {lastDaysFilter}
            OPTION (RECOMPILE);
            """;
    }

    internal static SupRoomInfo MapSupplierRoom(SupplierRoomRow row)
        => new()
        {
            Id = row.Id,
            SupId = row.SupId,
            SHotelCode = row.SHotelCode,
            SRoomCode = row.SRoomCode,
            MTHotelCode = row.MTHotelCode,
            SRoomNameCN = row.SRoomNameCN,
            SRoomNameEN = row.SRoomNameEN,
            BedInfo = row.BedInfo ?? string.Empty,
            AcreageFrom = row.AcreageFrom,
            AcreageTo = row.AcreageTo,
            SmokeType = row.SmokeType,
            MaxAdt = row.MaxAdt,
            BedInfoKey = string.Empty
        };

    public async Task<IReadOnlyList<OTARoomInfo>> LoadOtaRoomsAsync(
        IReadOnlyCollection<string> hotelCodes,
        CancellationToken cancellationToken)
    {
        var hotelCodeParameter = CreateHotelCodesParameter(hotelCodes);
        if (hotelCodeParameter is null)
        {
            return [];
        }

        var sql = """
            SELECT
                a.Id,
                a.MTHotelCode,
                a.RoomNameCN,
                a.RoomNameEN
            FROM dbo.OTARoomInfo AS a WITH (NOLOCK)
            WHERE a.MTHotelCode IN (SELECT Value FROM @MTHotelCodes)
            OPTION (RECOMPILE);
            """;

        var rows = await _dbContext.Database
            .SqlQueryRaw<OtaRoomRow>(sql, hotelCodeParameter)
            .ToListAsync(cancellationToken);

        return rows.Select(static row => new OTARoomInfo
        {
            Id = row.Id,
            MTHotelCode = row.MTHotelCode,
            RoomNameCN = row.RoomNameCN,
            RoomNameEN = row.RoomNameEN,
            BedInfo = string.Empty,
            DefaultWord = string.Empty
        }).ToList();
    }

    public Task BulkInsertOtaRoomInfoPresAsync(
        IReadOnlyList<OTARoomInfoPre> rooms,
        CancellationToken cancellationToken)
    {
        return _dbContext.BulkInsertAsync(rooms.ToList(), cancellationToken: cancellationToken);
    }

    static string BuildSupIdFilter(IReadOnlyCollection<int> enabledSupIds)
    {
        var supIds = enabledSupIds
            .Where(static supId => supId > 0)
            .Distinct()
            .ToArray();

        return string.Join(",", supIds.Length == 0 ? [51, 32] : supIds);
    }

    static SqlParameter? CreateHotelCodesParameter(IReadOnlyCollection<string> hotelCodes)
    {
        var normalizedCodes = hotelCodes
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Select(static code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return null;
        }

        var table = new DataTable();
        table.Columns.Add("Value", typeof(string));
        foreach (var code in normalizedCodes)
        {
            table.Rows.Add(code);
        }

        return new SqlParameter("@MTHotelCodes", SqlDbType.Structured)
        {
            TypeName = "dbo.StringList",
            Value = table
        };
    }

    sealed class IncrementalHotelRow
    {
        public string MTHotelCode { get; set; } = string.Empty;

        public DateTime LastUpdateDate { get; set; }

        public int Id { get; set; }
    }

    internal sealed class SupplierRoomRow
    {
        public int Id { get; set; }

        public int SupId { get; set; }

        public string SHotelCode { get; set; } = string.Empty;

        public string SRoomCode { get; set; } = string.Empty;

        public string MTHotelCode { get; set; } = string.Empty;

        public string SRoomNameCN { get; set; } = string.Empty;

        public string? SRoomNameEN { get; set; }

        public string? BedInfo { get; set; }

        public decimal? AcreageFrom { get; set; }

        public decimal? AcreageTo { get; set; }

        public int? SmokeType { get; set; }

        public int? MaxAdt { get; set; }
    }

    sealed class OtaRoomRow
    {
        public int Id { get; set; }

        public string MTHotelCode { get; set; } = string.Empty;

        public string RoomNameCN { get; set; } = string.Empty;

        public string? RoomNameEN { get; set; }
    }
}

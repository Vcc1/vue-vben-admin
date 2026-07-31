using System.Globalization;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotelMatchingPipeline.Infra.Sql;

public sealed class RoomMappingHotelRepository :
    IHotelCleaningRepository,
    IHotelMatchPipelineRepository,
    IHotelMatchingRepository,
    IHotelMatchDeliveryRepository
{
    const byte DeliveryPending = 0;
    const byte DeliverySending = 1;
    const byte DeliveryDelivered = 2;
    const byte DeliveryFailed = 3;
    const int PendingSupplierHotelIdPageSize = 1_000;
    const int MatchingInformationStageSlowLogMilliseconds = 30_000;
    static readonly DateTime SqlDateTimeMinimum = new(1753, 1, 1);
    static readonly DateTime SqlDateTimeMaximum = new(9999, 12, 31, 23, 59, 59, 997);

    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    sealed record HotelMatchResultWriteRow(
        int SupHotelId,
        string MTHotelCode,
        int MatchingVersion,
        byte NameEvidenceSourceType,
        int NameEvidenceHotelId,
        byte NameMatchKind,
        string? NamePendingValue,
        string? NameEvidenceValue,
        byte CoordinateEvidenceSourceType,
        int CoordinateEvidenceHotelId,
        double DistanceMeters,
        byte? StarEvidenceSourceType,
        int? StarEvidenceHotelId,
        string? StarPendingValue,
        string? StarEvidenceValue,
        byte? AddressEvidenceSourceType,
        int? AddressEvidenceHotelId,
        string? AddressPendingValue,
        string? AddressEvidenceValue,
        byte? PhoneEvidenceSourceType,
        int? PhoneEvidenceHotelId,
        string? PhonePendingValue,
        string? PhoneEvidenceValue,
        int OtaFactCount,
        int SupplierFactCount,
        string? EvidenceJson);

    readonly RoomMappingDbContext _dbContext;
    readonly string _connectionString;
    readonly IHotelStarLevelNormalizer _starLevelNormalizer;
    readonly IHotelStarLevelIssueRecorder _starLevelIssueRecorder;
    readonly ILogger<RoomMappingHotelRepository> _logger;
    readonly HashSet<int> _sessionSkippedPlatformIds = [];
    readonly HashSet<int> _sessionSkippedSupplierIds = [];
    bool? _hotelMatchResultHasLegacyMatchingVersion;

    public RoomMappingHotelRepository(
        RoomMappingDbContext dbContext,
        IHotelStarLevelNormalizer starLevelNormalizer,
        IHotelStarLevelIssueRecorder starLevelIssueRecorder,
        ILogger<RoomMappingHotelRepository> logger)
    {
        _dbContext = dbContext;
        _connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("RoomMapping database connection string is not configured.");
        _starLevelNormalizer = starLevelNormalizer;
        _starLevelIssueRecorder = starLevelIssueRecorder;
        _logger = logger;
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
    }

    public async Task<HotelCleaningWorkSet> LoadCleaningWorkSetAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        int pendingIdBatchSize,
        Action<HotelCleaningScanProgress> reportScanProgress,
        Func<int, CancellationToken, ValueTask> enqueuePendingId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var idColumn = type == HotelDataType.Platform ? "HotelID" : "SupHotelId";
        var rawTable = type == HotelDataType.Platform ? "dbo.OTAHotelInfoRaw" : "dbo.SupHotelInfoRaw";
        var cleanTable = type == HotelDataType.Platform ? "dbo.OTAHotelInfo" : "dbo.SupHotelInfo";
        var sourceTotal = await GetEstimatedRowCountAsync(connection, rawTable, cancellationToken);
        reportScanProgress(new HotelCleaningScanProgress(sourceTotal, 0, 0, 0, 0));
        var indexState = await GetCleaningIndexStateAsync(
            connection,
            cleanTable,
            type == HotelDataType.Platform
                ? "IX_OTAHotelInfo_CleaningSnapshot"
                : "IX_SupHotelInfo_CleaningSnapshot",
            cancellationToken);
        _logger.LogInformation(
            "Hotel cleaning ID snapshot index state. Source={Source}, CleanSnapshotIndex={CleanSnapshotIndex}, FailureSnapshotIndex={FailureSnapshotIndex}",
            type,
            indexState.CleanSnapshot,
            indexState.FailureSnapshot);

        // Classify with covering indexes in SQL and return only the small pending-ID set.
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 900;
        command.CommandText = $"""
            SELECT raw.{idColumn}
            FROM {rawTable} AS raw
            LEFT MERGE JOIN {cleanTable} AS clean
                ON clean.{idColumn} = raw.{idColumn}
               AND clean.CleanVersion = @cleanVersion
               AND clean.RegionFenceVersion = @regionBoundaryVersion
               AND clean.SourceDataVersion = raw.DataVersion
               AND (raw.DataChangeTime < @changedSince OR clean.CleanTime >= raw.DataChangeTime)
            LEFT MERGE JOIN dbo.HotelCleaningFailure AS failure
                ON failure.SourceType = @sourceType
               AND failure.CleanVersion = @cleanVersion
               AND failure.RegionBoundaryVersion = @regionBoundaryVersion
               AND failure.DataId = raw.{idColumn}
            WHERE clean.{idColumn} IS NULL
              AND failure.DataId IS NULL
            ORDER BY raw.{idColumn}
            OPTION (MERGE JOIN, MAXDOP 4, RECOMPILE);

            SELECT COUNT_BIG(*)
            FROM dbo.HotelCleaningFailure AS failure
            INNER MERGE JOIN {rawTable} AS raw
                ON raw.{idColumn} = TRY_CONVERT(int, failure.DataId)
            WHERE failure.SourceType = @sourceType
              AND failure.CleanVersion = @cleanVersion
              AND failure.RegionBoundaryVersion = @regionBoundaryVersion
              AND failure.DataId BETWEEN 1 AND 2147483647
            OPTION (MERGE JOIN, MAXDOP 4, RECOMPILE);
            """;
        command.Parameters.Add(new SqlParameter("@sourceType", System.Data.SqlDbType.TinyInt)
        {
            Value = (byte)type
        });
        command.Parameters.Add(new SqlParameter("@cleanVersion", System.Data.SqlDbType.Int)
        {
            Value = cleanVersion
        });
        command.Parameters.Add(new SqlParameter("@regionBoundaryVersion", System.Data.SqlDbType.Int)
        {
            Value = regionBoundaryVersion
        });
        command.Parameters.Add(new SqlParameter("@changedSince", System.Data.SqlDbType.DateTime2)
        {
            Value = changedSince.LocalDateTime
        });

        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);

        var pending = 0L;
        var progressInterval = Math.Max(1, pendingIdBatchSize);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dataId = reader.GetInt32(0);
            await enqueuePendingId(dataId, cancellationToken);
            pending++;
            if (pending % progressInterval == 0)
            {
                var estimatedCleaned = Math.Max(0, sourceTotal - pending);
                reportScanProgress(new HotelCleaningScanProgress(
                    sourceTotal,
                    Math.Min(sourceTotal, estimatedCleaned + pending),
                    estimatedCleaned,
                    0,
                    pending));
            }
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var failed = reader.GetInt64(0);
        var total = Math.Max(sourceTotal, failed + pending);
        var cleaned = Math.Max(0, total - failed - pending);
        reportScanProgress(new HotelCleaningScanProgress(
            total,
            total,
            cleaned,
            failed,
            pending));

        return new HotelCleaningWorkSet(total, cleaned, failed);
    }

    static async Task<long> GetEstimatedRowCountAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = """
            SELECT COALESCE(SUM(row_count), 0)
            FROM sys.dm_db_partition_stats
            WHERE object_id = OBJECT_ID(@tableName)
              AND index_id IN (0, 1);
            """;
        command.Parameters.Add(new SqlParameter("@tableName", System.Data.SqlDbType.NVarChar, 256) { Value = tableName });
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    static async Task<(bool CleanSnapshot, bool FailureSnapshot)> GetCleaningIndexStateAsync(
        SqlConnection connection,
        string cleanTable,
        string cleanIndexName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = """
            SELECT
                CONVERT(bit, CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(@cleanTable)
                      AND name = @cleanIndexName
                ) THEN 1 ELSE 0 END),
                CONVERT(bit, CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.HotelCleaningFailure')
                      AND name = N'IX_HotelCleaningFailure_CleaningSnapshot'
                ) THEN 1 ELSE 0 END);
            """;
        command.Parameters.Add(new SqlParameter("@cleanTable", System.Data.SqlDbType.NVarChar, 256)
        {
            Value = cleanTable
        });
        command.Parameters.Add(new SqlParameter("@cleanIndexName", System.Data.SqlDbType.NVarChar, 128)
        {
            Value = cleanIndexName
        });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetBoolean(0), reader.GetBoolean(1));
    }

    public async Task<IReadOnlyList<RawHotelData>> GetHotelsByIdsAsync(
        HotelDataType type,
        IReadOnlyCollection<int> dataIds,
        CancellationToken cancellationToken)
    {
        if (dataIds.Count == 0)
        {
            return [];
        }

        var idsJson = JsonSerializer.Serialize(dataIds, JsonOptions);
        var idsParameter = new SqlParameter("@ids", System.Data.SqlDbType.NVarChar, -1) { Value = idsJson };
        var readOptions = new DbContextOptionsBuilder<RoomMappingDbContext>()
            .UseSqlServer(_connectionString, sql => sql.CommandTimeout(300))
            .Options;
        await using var readContext = new RoomMappingDbContext(readOptions);
        if (type == HotelDataType.Platform)
        {
            var rows = await readContext.OTAHotelInfoRaws
                .FromSqlRaw("""
                    SELECT raw.*
                    FROM OPENJSON(@ids) WITH (HotelID int '$') AS requested
                    INNER LOOP JOIN dbo.OTAHotelInfoRaw AS raw WITH (FORCESEEK)
                        ON raw.HotelID = requested.HotelID
                    ORDER BY raw.HotelID
                    OPTION (FORCE ORDER, LOOP JOIN, MAXDOP 1, RECOMPILE)
                    """, idsParameter)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return rows.Select(MapRaw).ToArray();
        }

        var supplierRows = await readContext.SupHotelInfoRaws
            .FromSqlRaw("""
                SELECT raw.*
                FROM OPENJSON(@ids) WITH (SupHotelId int '$') AS requested
                INNER LOOP JOIN dbo.SupHotelInfoRaw AS raw WITH (FORCESEEK)
                    ON raw.SupHotelId = requested.SupHotelId
                ORDER BY raw.SupHotelId
                OPTION (FORCE ORDER, LOOP JOIN, MAXDOP 1, RECOMPILE)
                """, idsParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return supplierRows.Select(MapRaw).ToArray();
    }

    public async Task<int> SaveCleanedHotelsAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        try
        {
            await SaveCleanedHotelsWithRetryAsync(results, cancellationToken);
            return results.Count;
        }
        catch (Exception batchException) when (batchException is not OperationCanceledException && !IsTransientSqlFailure(batchException))
        {
            _dbContext.ChangeTracker.Clear();
            if (results.Count == 1)
            {
                await RecordCleaningFailuresAsync([CreateFailureRecord(results.Single(), batchException)], cancellationToken);
                return 0;
            }

            var savedCount = 0;
            var failures = new List<HotelCleaningFailureRecord>();
            foreach (var result in results)
            {
                try
                {
                    await SaveCleanedHotelsWithRetryAsync([result], cancellationToken);
                    savedCount++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && !IsTransientSqlFailure(ex))
                {
                    _dbContext.ChangeTracker.Clear();
                    failures.Add(CreateFailureRecord(result, ex));
                }
                catch (Exception ex) when (IsTransientSqlFailure(ex))
                {
                    _dbContext.ChangeTracker.Clear();
                    SkipResultsForSession([result]);
                    _logger.LogError(
                        ex,
                        "Skipping cleaned hotel after transient SQL retries were exhausted. Type={Type}, DataId={DataId}, HotelCode={HotelCode}",
                        result.Raw.Type,
                        result.Raw.Id ?? result.Raw.PlatformHotelId,
                        result.Raw.HotelCode);
                    failures.Add(CreateFailureRecord(result, ex));
                }
            }

            await RecordCleaningFailuresAsync(failures, cancellationToken);

            return savedCount;
        }
        catch (Exception ex) when (IsTransientSqlFailure(ex))
        {
            _dbContext.ChangeTracker.Clear();
            SkipResultsForSession(results);
            _logger.LogError(
                ex,
                "Skipping cleaned hotel batch after transient SQL retries were exhausted. BatchSize={BatchSize}",
                results.Count);
            await RecordCleaningFailuresAsync(
                results.Select(result => CreateFailureRecord(result, ex)).ToArray(),
                cancellationToken);
            return 0;
        }
    }

    async Task SaveCleanedHotelsWithRetryAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        CancellationToken cancellationToken)
    {
        const int maximumRetries = 5;
        var retry = 0;
        while (true)
        {
            try
            {
                await SaveCleanedHotelsCoreAsync(results, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsTransientSqlFailure(ex) && retry < maximumRetries)
            {
                retry++;
                _dbContext.ChangeTracker.Clear();
                await _dbContext.Database.CloseConnectionAsync();
                var delay = TimeSpan.FromSeconds(retry * 2);
                _logger.LogWarning(
                    ex,
                    "Transient SQL failure while saving cleaned hotels. Retry={Retry}, MaximumRetries={MaximumRetries}, BatchSize={BatchSize}, RetryDelaySeconds={RetryDelaySeconds}",
                    retry,
                    maximumRetries,
                    results.Count,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    static bool IsTransientSqlFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is
                    -2 or 1205 or 10928 or 10929 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920))
            {
                return true;
            }
        }

        return false;
    }

    async Task SaveCleanedHotelsCoreAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        foreach (var group in results.GroupBy(x => x.Raw.Type))
        {
            if (group.Key == HotelDataType.Platform)
            {
                await BulkUpsertOtaHotelsAsync(group.ToArray(), now, cancellationToken);
            }
            else
            {
                await BulkUpsertSupplierHotelsAsync(group.ToArray(), now, cancellationToken);
            }
        }

        _dbContext.ChangeTracker.Clear();
    }

    async Task BulkUpsertOtaHotelsAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rows = results.Select(result =>
        {
            var entity = new OTAHotelInfo { CreateTime = now };
            Map(entity, result, now);
            if (entity.HotelID <= 0)
            {
                throw new InvalidOperationException("OTA raw row is missing HotelID.");
            }

            return new
            {
                hotelId = entity.HotelID,
                mtHotelCode = entity.MTHotelCode,
                hotelName = entity.HotelName,
                hotelEnName = entity.HotelEnName,
                cityCode = entity.CityCode,
                address = entity.Address,
                enAddress = entity.EnAddress,
                estate = entity.Estate,
                flag = entity.Flag,
                areaId = entity.AreaID,
                countryId = entity.CountryID,
                provinceId = entity.ProvinceID,
                rootCityId = entity.RootCityID,
                cityId = entity.CityID,
                countryName = entity.CountryName,
                countryCode = entity.CountryCode,
                countryNameEn = entity.CountryNameEn,
                cityName = entity.CityName,
                cityNameEn = entity.CityNameEn,
                tel = entity.Tel,
                fax = entity.Fax,
                postCode = entity.PostCode,
                email = entity.Email,
                webSite = entity.WebSite,
                starLevel = entity.StarLevel,
                coordType = entity.CoordType,
                longitude = entity.Longitude,
                latitude = entity.Latitude,
                sourceLastModifyTime = FormatSqlDateTime(entity.SourceLastModifyTime, entity.HotelID),
                cleanVersion = entity.CleanVersion,
                sourceDataVersion = entity.SourceDataVersion,
                sourceDataHash = result.SourceDataHash,
                normalizedDataHash = result.NormalizedDataHash,
                regionLevel1Code = entity.RegionLevel1Code,
                regionLevel1Name = entity.RegionLevel1Name,
                regionLevel2Code = entity.RegionLevel2Code,
                regionLevel2Name = entity.RegionLevel2Name,
                regionLevel3Code = entity.RegionLevel3Code,
                regionLevel3Name = entity.RegionLevel3Name,
                regionResolveStatus = entity.RegionResolveStatus,
                regionFenceVersion = entity.RegionFenceVersion,
                cleanStatus = entity.CleanStatus,
                cleanError = entity.CleanError,
                dataQualityStatus = entity.DataQualityStatus,
                dataQualityFlags = entity.DataQualityFlags,
                dataQualityDetailsJson = entity.DataQualityDetailsJson,
                phones = result.Cleaned.NormalizedPhoneDetails.Select((phone, index) => new
                {
                    ordinal = checked((short)index),
                    sourcePhone = phone.Raw,
                    normalizedPhone = phone.E164,
                    countryIsoCode = phone.RegionCode,
                    nationalNumber = phone.NationalNumber,
                    countryCallingCode = phone.CountryCallingCode,
                    numberType = phone.NumberType,
                    areaCode = phone.AreaCode,
                    extension = phone.Extension,
                    isValid = phone.IsValid,
                    parseError = phone.Error
                })
            };
        }).ToArray();
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        var payloadParameter = new SqlParameter("@payload", System.Data.SqlDbType.NVarChar, -1) { Value = json };
        var nowParameter = new SqlParameter("@now", System.Data.SqlDbType.DateTime2) { Value = now };

        await _dbContext.Database.ExecuteSqlRawAsync("""
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            BEGIN TRY
                MERGE dbo.OTAHotelInfo WITH (HOLDLOCK, ROWLOCK) AS target
                USING
                (
                    SELECT *
                    FROM OPENJSON(@payload)
                    WITH
                    (
                        HotelID int '$.hotelId',
                        MTHotelCode varchar(100) '$.mtHotelCode',
                        HotelName nvarchar(200) '$.hotelName',
                        HotelEnName nvarchar(300) '$.hotelEnName',
                        CityCode varchar(90) '$.cityCode',
                        Address nvarchar(300) '$.address',
                        EnAddress nvarchar(500) '$.enAddress',
                        Estate int '$.estate',
                        Flag tinyint '$.flag',
                        AreaID int '$.areaId',
                        CountryID int '$.countryId',
                        ProvinceID int '$.provinceId',
                        RootCityID int '$.rootCityId',
                        CityID int '$.cityId',
                        CountryName varchar(64) '$.countryName',
                        CountryCode varchar(128) '$.countryCode',
                        CountryNameEn nvarchar(100) '$.countryNameEn',
                        CityName varchar(64) '$.cityName',
                        CityNameEn nvarchar(100) '$.cityNameEn',
                        Tel nvarchar(60) '$.tel',
                        Fax nvarchar(60) '$.fax',
                        PostCode varchar(50) '$.postCode',
                        Email varchar(100) '$.email',
                        WebSite varchar(500) '$.webSite',
                        StarLevel decimal(3,1) '$.starLevel',
                        CoordType tinyint '$.coordType',
                        Longitude decimal(10,7) '$.longitude',
                        Latitude decimal(10,7) '$.latitude',
                        SourceLastModifyTime datetime '$.sourceLastModifyTime',
                        CleanVersion int '$.cleanVersion',
                        SourceDataVersion bigint '$.sourceDataVersion',
                        SourceDataHash varchar(64) '$.sourceDataHash',
                        NormalizedDataHash varchar(64) '$.normalizedDataHash',
                        RegionLevel1Code varchar(50) '$.regionLevel1Code',
                        RegionLevel1Name nvarchar(100) '$.regionLevel1Name',
                        RegionLevel2Code varchar(50) '$.regionLevel2Code',
                        RegionLevel2Name nvarchar(100) '$.regionLevel2Name',
                        RegionLevel3Code varchar(50) '$.regionLevel3Code',
                        RegionLevel3Name nvarchar(100) '$.regionLevel3Name',
                        RegionResolveStatus tinyint '$.regionResolveStatus',
                        RegionFenceVersion int '$.regionFenceVersion',
                        CleanStatus tinyint '$.cleanStatus',
                        CleanError nvarchar(1000) '$.cleanError',
                        DataQualityStatus tinyint '$.dataQualityStatus',
                        DataQualityFlags bigint '$.dataQualityFlags',
                        DataQualityDetailsJson nvarchar(max) '$.dataQualityDetailsJson'
                    )
                ) AS source ON source.HotelID = target.HotelID
                WHEN MATCHED THEN UPDATE SET
                    target.MTHotelCode = source.MTHotelCode,
                    target.HotelName = source.HotelName,
                    target.HotelEnName = source.HotelEnName,
                    target.CityCode = source.CityCode,
                    target.Address = source.Address,
                    target.EnAddress = source.EnAddress,
                    target.Estate = source.Estate,
                    target.Flag = source.Flag,
                    target.AreaID = source.AreaID,
                    target.CountryID = source.CountryID,
                    target.ProvinceID = source.ProvinceID,
                    target.RootCityID = source.RootCityID,
                    target.CityID = source.CityID,
                    target.CountryName = source.CountryName,
                    target.CountryCode = source.CountryCode,
                    target.CountryNameEn = source.CountryNameEn,
                    target.CityName = source.CityName,
                    target.CityNameEn = source.CityNameEn,
                    target.Tel = source.Tel,
                    target.Fax = source.Fax,
                    target.PostCode = source.PostCode,
                    target.Email = source.Email,
                    target.WebSite = source.WebSite,
                    target.StarLevel = source.StarLevel,
                    target.CoordType = source.CoordType,
                    target.Longitude = source.Longitude,
                    target.Latitude = source.Latitude,
                    target.SourceLastModifyTime = source.SourceLastModifyTime,
                    target.CleanVersion = source.CleanVersion,
                    target.CleanTime = @now,
                    target.UpdateTime = @now,
                    target.SourceDataVersion = source.SourceDataVersion,
                    target.SourceDataHash = CONVERT(varbinary(32), source.SourceDataHash, 2),
                    target.NormalizedDataHash = CONVERT(varbinary(32), source.NormalizedDataHash, 2),
                    target.RegionLevel1Code = source.RegionLevel1Code,
                    target.RegionLevel1Name = source.RegionLevel1Name,
                    target.RegionLevel2Code = source.RegionLevel2Code,
                    target.RegionLevel2Name = source.RegionLevel2Name,
                    target.RegionLevel3Code = source.RegionLevel3Code,
                    target.RegionLevel3Name = source.RegionLevel3Name,
                    target.RegionResolveStatus = source.RegionResolveStatus,
                    target.RegionFenceVersion = source.RegionFenceVersion,
                    target.CleanStatus = source.CleanStatus,
                    target.CleanError = source.CleanError,
                    target.DataQualityStatus = source.DataQualityStatus,
                    target.DataQualityFlags = source.DataQualityFlags,
                    target.DataQualityDetailsJson = source.DataQualityDetailsJson
                WHEN NOT MATCHED THEN INSERT
                (
                    HotelID, MTHotelCode, HotelName, HotelEnName, CityCode, Address, EnAddress,
                    Estate, Flag, AreaID, CountryID, ProvinceID, RootCityID, CityID,
                    CountryName, CountryCode, CountryNameEn, CityName, CityNameEn, Tel, Fax,
                    PostCode, Email, WebSite, StarLevel, CoordType, Longitude, Latitude,
                    SourceLastModifyTime, CleanVersion, CleanTime, CreateTime, UpdateTime,
                    SourceDataVersion, SourceDataHash, NormalizedDataHash,
                    RegionLevel1Code, RegionLevel1Name, RegionLevel2Code, RegionLevel2Name,
                    RegionLevel3Code, RegionLevel3Name, RegionResolveStatus, RegionFenceVersion,
                    CleanStatus, CleanError, DataQualityStatus, DataQualityFlags, DataQualityDetailsJson
                )
                VALUES
                (
                    source.HotelID, source.MTHotelCode, source.HotelName, source.HotelEnName,
                    source.CityCode, source.Address, source.EnAddress, source.Estate, source.Flag,
                    source.AreaID, source.CountryID, source.ProvinceID, source.RootCityID, source.CityID,
                    source.CountryName, source.CountryCode, source.CountryNameEn, source.CityName,
                    source.CityNameEn, source.Tel, source.Fax, source.PostCode, source.Email,
                    source.WebSite, source.StarLevel, source.CoordType, source.Longitude, source.Latitude,
                    source.SourceLastModifyTime, source.CleanVersion, @now, @now,
                    @now, source.SourceDataVersion,
                    CONVERT(varbinary(32), source.SourceDataHash, 2),
                    CONVERT(varbinary(32), source.NormalizedDataHash, 2),
                    source.RegionLevel1Code, source.RegionLevel1Name, source.RegionLevel2Code,
                    source.RegionLevel2Name, source.RegionLevel3Code, source.RegionLevel3Name,
                    source.RegionResolveStatus, source.RegionFenceVersion, source.CleanStatus,
                    source.CleanError, source.DataQualityStatus, source.DataQualityFlags,
                    source.DataQualityDetailsJson
                )
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                DELETE phone
                FROM dbo.OTAHotelPhone AS phone WITH (ROWLOCK)
                INNER JOIN OPENJSON(@payload)
                    WITH (HotelID int '$.hotelId') AS hotel ON hotel.HotelID = phone.HotelID
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                INSERT dbo.OTAHotelPhone WITH (ROWLOCK)
                (
                    HotelID, Ordinal, SourcePhone, NormalizedPhone, CountryIsoCode,
                    NationalNumber, CountryCallingCode, NumberType, AreaCode, Extension,
                    IsValid, ParseError, CreateTime, UpdateTime
                )
                SELECT
                    hotel.HotelID, phone.Ordinal, phone.SourcePhone, phone.NormalizedPhone,
                    phone.CountryIsoCode, phone.NationalNumber, phone.CountryCallingCode,
                    phone.NumberType, phone.AreaCode, phone.Extension, phone.IsValid,
                    phone.ParseError, @now, @now
                FROM OPENJSON(@payload)
                    WITH (HotelID int '$.hotelId', Phones nvarchar(max) '$.phones' AS JSON) AS hotel
                CROSS APPLY OPENJSON(hotel.Phones)
                    WITH
                    (
                        Ordinal smallint '$.ordinal',
                        SourcePhone nvarchar(100) '$.sourcePhone',
                        NormalizedPhone varchar(32) '$.normalizedPhone',
                        CountryIsoCode char(2) '$.countryIsoCode',
                        NationalNumber varchar(32) '$.nationalNumber',
                        CountryCallingCode varchar(8) '$.countryCallingCode',
                        NumberType varchar(32) '$.numberType',
                        AreaCode varchar(16) '$.areaCode',
                        Extension varchar(20) '$.extension',
                        IsValid bit '$.isValid',
                        ParseError varchar(200) '$.parseError'
                    ) AS phone;

                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """, [payloadParameter, nowParameter], cancellationToken);
    }

    async Task BulkUpsertSupplierHotelsAsync(
        IReadOnlyCollection<HotelCleaningResult> results,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rows = results.Select(result =>
        {
            var supHotelId = checked((int)(result.Raw.Id ?? 0));
            if (supHotelId <= 0)
            {
                throw new InvalidOperationException("Supplier raw row is missing SupHotelId.");
            }

            var entity = new SupHotelInfoClean { SupHotelId = supHotelId, CreateTime = now };
            Map(entity, result, now);
            return new
            {
                supHotelId,
                supId = entity.SupId,
                sHotelCode = entity.SHotelCode,
                mtHotelCode = entity.MTHotelCode,
                state = entity.State,
                sSaleTopLevel = entity.SSaleTopLevel,
                isHandMatch = entity.IsHandMatch,
                reMatchState = entity.ReMatchState,
                hotelName = entity.HotelName,
                hotelNameEn = entity.HotelNameEn,
                countryCode = entity.CountryCode,
                countryName = entity.CountryName,
                cityCode = entity.CityCode,
                cityName = entity.CityName,
                tel = entity.Tel,
                fax = entity.Fax,
                address = entity.Address,
                postCode = entity.PostCode,
                email = entity.Email,
                starLevel = entity.StarLevel,
                coordType = entity.CoordType,
                longitude = entity.Longitude,
                latitude = entity.Latitude,
                regionLevel1Code = entity.RegionLevel1Code,
                regionLevel1Name = entity.RegionLevel1Name,
                regionLevel2Code = entity.RegionLevel2Code,
                regionLevel2Name = entity.RegionLevel2Name,
                regionLevel3Code = entity.RegionLevel3Code,
                regionLevel3Name = entity.RegionLevel3Name,
                regionResolveStatus = entity.RegionResolveStatus,
                regionFenceVersion = entity.RegionFenceVersion,
                sourceDataVersion = entity.SourceDataVersion,
                sourceDataHash = result.SourceDataHash,
                normalizedDataHash = result.NormalizedDataHash,
                cleanVersion = entity.CleanVersion,
                cleanStatus = entity.CleanStatus,
                cleanError = entity.CleanError,
                dataQualityStatus = entity.DataQualityStatus,
                dataQualityFlags = entity.DataQualityFlags,
                dataQualityDetailsJson = entity.DataQualityDetailsJson,
                isMatchable = result.IsMatchable,
                phones = result.Cleaned.NormalizedPhoneDetails.Select((phone, index) => new
                {
                    ordinal = checked((short)index),
                    raw = phone.Raw,
                    e164 = phone.E164,
                    nationalNumber = phone.NationalNumber,
                    countryCallingCode = phone.CountryCallingCode,
                    regionCode = phone.RegionCode,
                    numberType = phone.NumberType,
                    areaCode = phone.AreaCode,
                    extension = phone.Extension,
                    isValid = phone.IsValid,
                    error = phone.Error
                })
            };
        }).ToArray();
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        var payloadParameter = new SqlParameter("@payload", System.Data.SqlDbType.NVarChar, -1) { Value = json };
        var nowParameter = new SqlParameter("@now", System.Data.SqlDbType.DateTime2) { Value = now };
        var dataProblemParameter = new SqlParameter("@dataProblem", System.Data.SqlDbType.TinyInt)
        {
            Value = (byte)HotelMatchStatus.DataProblem
        };
        var pendingParameter = new SqlParameter("@pending", System.Data.SqlDbType.TinyInt)
        {
            Value = (byte)HotelMatchStatus.Pending
        };

        await _dbContext.Database.ExecuteSqlRawAsync("""
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            BEGIN TRY
                MERGE dbo.SupHotelInfo WITH (HOLDLOCK, ROWLOCK) AS target
                USING
                (
                    SELECT *
                    FROM OPENJSON(@payload)
                    WITH
                    (
                        SupHotelId int '$.supHotelId',
                        SupId int '$.supId',
                        SHotelCode nvarchar(50) '$.sHotelCode',
                        MTHotelCode varchar(100) '$.mtHotelCode',
                        State int '$.state',
                        SSaleTopLevel int '$.sSaleTopLevel',
                        IsHandMatch int '$.isHandMatch',
                        ReMatchState int '$.reMatchState',
                        HotelName nvarchar(200) '$.hotelName',
                        HotelNameEn nvarchar(200) '$.hotelNameEn',
                        CountryCode nvarchar(50) '$.countryCode',
                        CountryName nvarchar(50) '$.countryName',
                        CityCode nvarchar(50) '$.cityCode',
                        CityName nvarchar(100) '$.cityName',
                        Tel nvarchar(100) '$.tel',
                        Fax nvarchar(100) '$.fax',
                        Address nvarchar(500) '$.address',
                        PostCode varchar(50) '$.postCode',
                        Email varchar(100) '$.email',
                        StarLevel decimal(3,1) '$.starLevel',
                        CoordType tinyint '$.coordType',
                        Longitude decimal(10,7) '$.longitude',
                        Latitude decimal(10,7) '$.latitude',
                        RegionLevel1Code varchar(50) '$.regionLevel1Code',
                        RegionLevel1Name nvarchar(100) '$.regionLevel1Name',
                        RegionLevel2Code varchar(50) '$.regionLevel2Code',
                        RegionLevel2Name nvarchar(100) '$.regionLevel2Name',
                        RegionLevel3Code varchar(50) '$.regionLevel3Code',
                        RegionLevel3Name nvarchar(100) '$.regionLevel3Name',
                        RegionResolveStatus tinyint '$.regionResolveStatus',
                        RegionFenceVersion int '$.regionFenceVersion',
                        SourceDataVersion bigint '$.sourceDataVersion',
                        SourceDataHash varchar(64) '$.sourceDataHash',
                        NormalizedDataHash varchar(64) '$.normalizedDataHash',
                        CleanVersion int '$.cleanVersion',
                        CleanStatus tinyint '$.cleanStatus',
                        CleanError nvarchar(1000) '$.cleanError',
                        DataQualityStatus tinyint '$.dataQualityStatus',
                        DataQualityFlags bigint '$.dataQualityFlags',
                        DataQualityDetailsJson nvarchar(max) '$.dataQualityDetailsJson',
                        IsMatchable bit '$.isMatchable'
                    )
                ) AS source ON source.SupHotelId = target.SupHotelId
                WHEN MATCHED THEN UPDATE SET
                    target.SupId = source.SupId,
                    target.SHotelCode = source.SHotelCode,
                    target.MTHotelCode = source.MTHotelCode,
                    target.State = source.State,
                    target.SSaleTopLevel = source.SSaleTopLevel,
                    target.IsHandMatch = source.IsHandMatch,
                    target.ReMatchState = source.ReMatchState,
                    target.HotelName = source.HotelName,
                    target.HotelNameEn = source.HotelNameEn,
                    target.CountryCode = source.CountryCode,
                    target.CountryName = source.CountryName,
                    target.CityCode = source.CityCode,
                    target.CityName = source.CityName,
                    target.Tel = source.Tel,
                    target.Fax = source.Fax,
                    target.Address = source.Address,
                    target.PostCode = source.PostCode,
                    target.Email = source.Email,
                    target.StarLevel = source.StarLevel,
                    target.CoordType = source.CoordType,
                    target.Longitude = source.Longitude,
                    target.Latitude = source.Latitude,
                    target.RegionLevel1Code = source.RegionLevel1Code,
                    target.RegionLevel1Name = source.RegionLevel1Name,
                    target.RegionLevel2Code = source.RegionLevel2Code,
                    target.RegionLevel2Name = source.RegionLevel2Name,
                    target.RegionLevel3Code = source.RegionLevel3Code,
                    target.RegionLevel3Name = source.RegionLevel3Name,
                    target.RegionResolveStatus = source.RegionResolveStatus,
                    target.RegionFenceVersion = source.RegionFenceVersion,
                    target.SourceDataVersion = source.SourceDataVersion,
                    target.SourceDataHash = CONVERT(varbinary(32), source.SourceDataHash, 2),
                    target.NormalizedDataHash = CONVERT(varbinary(32), source.NormalizedDataHash, 2),
                    target.CleanVersion = source.CleanVersion,
                    target.CleanStatus = source.CleanStatus,
                    target.CleanError = source.CleanError,
                    target.CleanTime = @now,
                    target.DataQualityStatus = source.DataQualityStatus,
                    target.DataQualityFlags = source.DataQualityFlags,
                    target.DataQualityDetailsJson = source.DataQualityDetailsJson,
                    target.MatchStatus = CASE
                        WHEN source.IsMatchable = 0 THEN @dataProblem
                        WHEN ISNULL(target.NormalizedDataHash, 0x)
                             <> ISNULL(CONVERT(varbinary(32), source.NormalizedDataHash, 2), 0x)
                             AND NULLIF(LTRIM(RTRIM(source.MTHotelCode)), '') IS NULL
                            THEN @pending
                        ELSE target.MatchStatus
                    END,
                    target.MatchError = CASE
                        WHEN source.IsMatchable = 1
                             AND ISNULL(target.NormalizedDataHash, 0x)
                                 <> ISNULL(CONVERT(varbinary(32), source.NormalizedDataHash, 2), 0x)
                             AND NULLIF(LTRIM(RTRIM(source.MTHotelCode)), '') IS NULL
                            THEN NULL
                        ELSE target.MatchError
                    END,
                    target.UpdateTime = @now
                WHEN NOT MATCHED THEN INSERT
                (
                    SupHotelId, SupId, SHotelCode, MTHotelCode, State, SSaleTopLevel,
                    IsHandMatch, ReMatchState, HotelName, HotelNameEn, Tel, Fax, Address,
                    CountryCode, CountryName, CityCode, CityName,
                    PostCode, Email, StarLevel, CoordType, Longitude, Latitude,
                    RegionLevel1Code, RegionLevel1Name, RegionLevel2Code, RegionLevel2Name,
                    RegionLevel3Code, RegionLevel3Name, RegionResolveStatus, RegionFenceVersion,
                    SourceDataVersion, SourceDataHash, NormalizedDataHash, CleanVersion,
                    CleanStatus, CleanError, CleanTime, DataQualityStatus, DataQualityFlags,
                    DataQualityDetailsJson, MatchStatus, CreateTime, UpdateTime
                )
                VALUES
                (
                    source.SupHotelId, source.SupId, source.SHotelCode, source.MTHotelCode,
                    source.State, source.SSaleTopLevel, source.IsHandMatch, source.ReMatchState,
                    source.HotelName, source.HotelNameEn, source.Tel, source.Fax, source.Address,
                    source.CountryCode, source.CountryName, source.CityCode, source.CityName,
                    source.PostCode, source.Email, source.StarLevel, source.CoordType,
                    source.Longitude, source.Latitude, source.RegionLevel1Code, source.RegionLevel1Name,
                    source.RegionLevel2Code, source.RegionLevel2Name, source.RegionLevel3Code,
                    source.RegionLevel3Name, source.RegionResolveStatus, source.RegionFenceVersion,
                    source.SourceDataVersion, CONVERT(varbinary(32), source.SourceDataHash, 2),
                    CONVERT(varbinary(32), source.NormalizedDataHash, 2), source.CleanVersion,
                    source.CleanStatus, source.CleanError, @now, source.DataQualityStatus,
                    source.DataQualityFlags, source.DataQualityDetailsJson,
                    CASE WHEN source.IsMatchable = 1 THEN @pending ELSE @dataProblem END,
                    @now, @now
                )
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                DELETE phone
                FROM dbo.SupHotelPhone AS phone WITH (ROWLOCK)
                INNER JOIN OPENJSON(@payload)
                    WITH (SupHotelId int '$.supHotelId') AS hotel
                    ON hotel.SupHotelId = phone.SupHotelId
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                INSERT dbo.SupHotelPhone WITH (ROWLOCK)
                (
                    SupHotelId, Ordinal, Raw, E164, NationalNumber, CountryCallingCode,
                    RegionCode, NumberType, AreaCode, Extension, IsValid, Error,
                    CreateTime, UpdateTime
                )
                SELECT
                    hotel.SupHotelId, phone.Ordinal, phone.Raw, phone.E164,
                    phone.NationalNumber, phone.CountryCallingCode, phone.RegionCode,
                    phone.NumberType, phone.AreaCode, phone.Extension, phone.IsValid,
                    phone.Error, @now, @now
                FROM OPENJSON(@payload)
                    WITH (SupHotelId int '$.supHotelId', Phones nvarchar(max) '$.phones' AS JSON) AS hotel
                CROSS APPLY OPENJSON(hotel.Phones)
                    WITH
                    (
                        Ordinal smallint '$.ordinal',
                        Raw nvarchar(100) '$.raw',
                        E164 varchar(32) '$.e164',
                        NationalNumber varchar(32) '$.nationalNumber',
                        CountryCallingCode varchar(8) '$.countryCallingCode',
                        RegionCode char(2) '$.regionCode',
                        NumberType varchar(32) '$.numberType',
                        AreaCode varchar(16) '$.areaCode',
                        Extension varchar(20) '$.extension',
                        IsValid bit '$.isValid',
                        Error varchar(100) '$.error'
                    ) AS phone;

                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """,
            [payloadParameter, nowParameter, dataProblemParameter, pendingParameter],
            cancellationToken);
    }

    public async Task RecordCleaningFailuresAsync(
        IReadOnlyCollection<HotelCleaningFailureRecord> failures,
        CancellationToken cancellationToken)
    {
        if (failures.Count == 0)
        {
            return;
        }

        const int maximumRetries = 5;
        var retry = 0;
        while (true)
        {
            try
            {
                await RecordCleaningFailuresCoreAsync(failures, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsTransientSqlFailure(ex) && retry < maximumRetries)
            {
                retry++;
                _dbContext.ChangeTracker.Clear();
                await _dbContext.Database.CloseConnectionAsync();
                var delay = TimeSpan.FromSeconds(retry * 2);
                _logger.LogWarning(
                    ex,
                    "Transient SQL failure while recording cleaning failures. Retry={Retry}, MaximumRetries={MaximumRetries}, FailureCount={FailureCount}, RetryDelaySeconds={RetryDelaySeconds}",
                    retry,
                    maximumRetries,
                    failures.Count,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex) when (IsTransientSqlFailure(ex))
            {
                _dbContext.ChangeTracker.Clear();
                SkipFailuresForSession(failures);
                _logger.LogError(
                    ex,
                    "Unable to persist cleaning failures after transient SQL retries were exhausted. FailureCount={FailureCount}; rows are skipped for the current process only.",
                    failures.Count);
                return;
            }
        }
    }

    async Task RecordCleaningFailuresCoreAsync(
        IReadOnlyCollection<HotelCleaningFailureRecord> failures,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var rows = failures
            .GroupBy(x => new { x.SourceType, x.DataId })
            .Select(group => group.Last())
            .Select(failure => new
            {
                sourceType = (byte)failure.SourceType,
                dataId = failure.DataId,
                supplierId = failure.SupplierId,
                sourceId = failure.SourceId,
                hotelCode = Truncate(failure.HotelCode, 200),
                exceptionType = Truncate(failure.ExceptionType, 500),
                exceptionMessage = Truncate(failure.ExceptionMessage, 4000),
                stackTrace = failure.StackTrace,
                cleanVersion = failure.CleanVersion,
                regionBoundaryVersion = failure.RegionBoundaryVersion
            })
            .ToArray();
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        var payloadParameter = new SqlParameter("@payload", System.Data.SqlDbType.NVarChar, -1) { Value = json };
        var nowParameter = new SqlParameter("@now", System.Data.SqlDbType.DateTime2) { Value = now };

        await _dbContext.Database.ExecuteSqlRawAsync("""
            SET NOCOUNT ON;

            MERGE dbo.HotelCleaningFailure WITH (HOLDLOCK) AS target
            USING
            (
                SELECT *
                FROM OPENJSON(@payload)
                WITH
                (
                    SourceType tinyint '$.sourceType',
                    DataId bigint '$.dataId',
                    SupplierId int '$.supplierId',
                    SourceId int '$.sourceId',
                    HotelCode nvarchar(200) '$.hotelCode',
                    ExceptionType varchar(500) '$.exceptionType',
                    ExceptionMessage nvarchar(4000) '$.exceptionMessage',
                    StackTrace nvarchar(max) '$.stackTrace',
                    CleanVersion int '$.cleanVersion',
                    RegionBoundaryVersion int '$.regionBoundaryVersion'
                )
            ) AS source
                ON source.SourceType = target.SourceType
               AND source.DataId = target.DataId
            WHEN MATCHED THEN UPDATE SET
                target.SupplierId = source.SupplierId,
                target.SourceId = source.SourceId,
                target.HotelCode = source.HotelCode,
                target.ExceptionType = source.ExceptionType,
                target.ExceptionMessage = source.ExceptionMessage,
                target.StackTrace = source.StackTrace,
                target.CleanVersion = source.CleanVersion,
                target.RegionBoundaryVersion = source.RegionBoundaryVersion,
                target.FailureCount = target.FailureCount + 1,
                target.LastFailureTime = @now
            WHEN NOT MATCHED THEN INSERT
            (
                SourceType, DataId, SupplierId, SourceId, HotelCode, ExceptionType,
                ExceptionMessage, StackTrace, CleanVersion, RegionBoundaryVersion,
                FailureCount, FirstFailureTime, LastFailureTime
            )
            VALUES
            (
                source.SourceType, source.DataId, source.SupplierId, source.SourceId,
                source.HotelCode, source.ExceptionType, source.ExceptionMessage,
                source.StackTrace, source.CleanVersion, source.RegionBoundaryVersion,
                1, @now, @now
            );
            """, [payloadParameter, nowParameter], cancellationToken);

        _dbContext.ChangeTracker.Clear();
    }

    public async Task LoadPendingSupplierHotelIdsAsync(
        Func<int, CancellationToken, ValueTask> enqueueId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 900;
        command.CommandText = """
            SELECT TOP (@pageSize) hotel.SupHotelId
            FROM dbo.SupHotelInfo AS hotel WITH (NOLOCK)
            WHERE hotel.State = 1
              AND hotel.MTHotelCode = ''
              AND hotel.SupHotelId > @lastSupHotelId
              AND ISNULL(LTRIM(RTRIM(hotel.RegionLevel1Code)), '') <> ''
              AND UPPER(LTRIM(RTRIM(hotel.RegionLevel1Code)))
                  NOT IN ('CN', 'CHN', 'TW', 'TWN', 'HK', 'HKG', 'MO', 'MAC')
            ORDER BY hotel.SupHotelId
            OPTION (MAXDOP 1, RECOMPILE);
            """;
        var pageSizeParameter = command.Parameters.Add(
            new SqlParameter("@pageSize", System.Data.SqlDbType.Int)
            {
                Value = PendingSupplierHotelIdPageSize
            });
        var lastSupHotelIdParameter = command.Parameters.Add(
            new SqlParameter("@lastSupHotelId", System.Data.SqlDbType.Int)
            {
                Value = 0
            });

        var lastSupHotelId = 0;
        while (true)
        {
            pageSizeParameter.Value = PendingSupplierHotelIdPageSize;
            lastSupHotelIdParameter.Value = lastSupHotelId;
            var ids = new List<int>(PendingSupplierHotelIdPageSize);
            await using (var reader = await command.ExecuteReaderAsync(
                System.Data.CommandBehavior.SequentialAccess,
                cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    ids.Add(reader.GetInt32(0));
                }
            }

            if (ids.Count == 0)
            {
                return;
            }

            foreach (var id in ids)
            {
                lastSupHotelId = id;
                await enqueueId(id, cancellationToken);
            }
        }
    }

    public async Task<HotelMatchingWorkItem> LoadMatchingWorkItemAsync(
        int supHotelId,
        int recallRadiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var pending = await RunMatchingInformationStageAsync(
            supHotelId,
            "pending_supplier",
            token => _dbContext.SupHotelInfos
                .AsNoTracking()
                .SingleAsync(
                    hotel => hotel.SupHotelId == supHotelId,
                    token),
            cancellationToken);
        if (!HotelRecallPolicy.IsMatchingEnabled(pending.RegionLevel1Code))
        {
            _logger.LogInformation(
                "Hotel matching skipped because RegionLevel1Code is missing or disabled. SupHotelId={SupHotelId}, RegionLevel1Code={RegionLevel1Code}",
                supHotelId,
                pending.RegionLevel1Code);
            return new HotelMatchingWorkItem(
                pending.SupHotelId,
                MapFact(pending),
                [],
                [],
                []);
        }

        var otaRows = new List<OTAHotelInfo>();
        var nearbySupplierRows = new List<SupHotelInfoClean>();
        if (pending.Longitude.HasValue && pending.Latitude.HasValue)
        {
            var recall = HotelRecallPolicy.Resolve(
                pending.RegionLevel1Code,
                pending.CountryCode,
                recallRadiusMeters,
                maxCandidates);
            otaRows = await RunMatchingInformationStageAsync(
                supHotelId,
                recall.MaxCandidates.HasValue ? "nearby_ota_by_distance" : "nearby_ota_by_grid",
                token => LoadNearbyOtaHotelsAsync(
                    pending.Latitude.Value,
                    pending.Longitude.Value,
                    recall.RadiusMeters,
                    recall.MaxCandidates,
                    token),
                cancellationToken);

            nearbySupplierRows = await RunMatchingInformationStageAsync(
                supHotelId,
                recall.MaxCandidates.HasValue
                    ? "nearby_supplier_by_distance"
                    : "nearby_supplier_by_grid",
                token => LoadNearbySupplierHotelsAsync(
                    supHotelId,
                    pending.Latitude.Value,
                    pending.Longitude.Value,
                    recall.RadiusMeters,
                    recall.MaxCandidates,
                    token),
                cancellationToken);
        }

        var mtHotelCodes = otaRows
            .Select(hotel => hotel.MTHotelCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var linkedSupplierRows = mtHotelCodes.Length == 0
            ? []
            : await RunMatchingInformationStageAsync(
                supHotelId,
                "linked_supplier",
                token => LoadLinkedSupplierHotelsAsync(
                    mtHotelCodes,
                    token),
                cancellationToken);
        linkedSupplierRows.RemoveAll(hotel =>
            !HotelRecallPolicy.IsMatchingEnabled(hotel.RegionLevel1Code));

        await RunMatchingInformationStageAsync(
            supHotelId,
            "matching_phones",
            async token =>
            {
                await LoadAndAttachMatchingPhonesAsync(
                    otaRows,
                    [pending, .. nearbySupplierRows, .. linkedSupplierRows],
                    token);
                return true;
            },
            cancellationToken);

        return new HotelMatchingWorkItem(
            pending.SupHotelId,
            MapFact(pending),
            otaRows.Select(MapFact).ToArray(),
            nearbySupplierRows.Select(MapFact).ToArray(),
            linkedSupplierRows.Select(MapFact).ToArray());
    }

    async Task<T> RunMatchingInformationStageAsync<T>(
        int supHotelId,
        string stage,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var slowLogCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var slowLogTask = LogMatchingInformationStageWhileRunningAsync(
            supHotelId,
            stage,
            stopwatch,
            slowLogCancellation.Token);
        try
        {
            var result = await action(cancellationToken);
            if (stopwatch.ElapsedMilliseconds >= MatchingInformationStageSlowLogMilliseconds)
            {
                _logger.LogWarning(
                    "Hotel matching information stage completed slowly. SupHotelId={SupHotelId}, Stage={Stage}, ElapsedMs={ElapsedMs}",
                    supHotelId,
                    stage,
                    stopwatch.ElapsedMilliseconds);
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hotel matching information stage failed. SupHotelId={SupHotelId}, Stage={Stage}, ElapsedMs={ElapsedMs}",
                supHotelId,
                stage,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            slowLogCancellation.Cancel();
            await slowLogTask;
        }
    }

    async Task LogMatchingInformationStageWhileRunningAsync(
        int supHotelId,
        string stage,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                if (!await DelayOrCanceledAsync(
                        TimeSpan.FromMilliseconds(MatchingInformationStageSlowLogMilliseconds),
                        cancellationToken))
                {
                    return;
                }
                _logger.LogWarning(
                    "Hotel matching information stage is still running. SupHotelId={SupHotelId}, Stage={Stage}, ElapsedMs={ElapsedMs}",
                    supHotelId,
                    stage,
                    stopwatch.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    async Task<List<OTAHotelInfo>> LoadNearbyOtaHotelsAsync(
        decimal latitude,
        decimal longitude,
        int recallRadiusMeters,
        int? maxCandidates,
        CancellationToken cancellationToken)
    {
        var rows = maxCandidates.HasValue
            ? await LoadNearbyOtaHotelsByDistanceAsync(
                latitude,
                longitude,
                recallRadiusMeters,
                maxCandidates.Value,
                cancellationToken)
            : await LoadNearbyOtaHotelsByGridAsync(
                latitude,
                longitude,
                recallRadiusMeters,
                cancellationToken);
        rows.RemoveAll(hotel =>
            !HotelRecallPolicy.IsMatchingEnabled(hotel.RegionLevel1Code));
        SortByDistance(
            rows,
            latitude,
            longitude,
            hotel => hotel.Latitude,
            hotel => hotel.Longitude,
            hotel => hotel.HotelID);
        return rows;
    }

    async Task<List<OTAHotelInfo>> LoadNearbyOtaHotelsByDistanceAsync(
        decimal latitude,
        decimal longitude,
        int recallRadiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var commandText = """
            DECLARE @origin geography = geography::Point(@latitude, @longitude, 4326);

            SELECT TOP (@maxCandidates)
                ota.HotelID,
                ota.MTHotelCode,
                ota.HotelName,
                ota.HotelEnName,
                ota.Longitude,
                ota.Latitude,
                ota.StarLevel,
                ota.Address,
                ota.RegionLevel1Code,
                ota.CountryCode,
                ota.CountryName,
                ota.CityCode,
                ota.CityName,
                ota.PostCode,
                ota.Tel,
                ota.Fax,
                ota.Email,
                ota.CoordType,
                ota.RegionLevel1Name,
                ota.RegionLevel2Code,
                ota.RegionLevel2Name,
                ota.RegionLevel3Code,
                ota.RegionLevel3Name,
                ota.RegionResolveStatus,
                ota.DataQualityStatus,
                ota.DataQualityFlags
            FROM dbo.OTAHotelInfo AS ota WITH (NOLOCK, INDEX(SIX_OTAHotelInfo_GeoPoint))
            WHERE ota.CleanStatus = @cleanStatus
              AND ota.DataQualityStatus = @dataQualityStatus
              AND ota.Estate = @onlineEstate
              AND ota.MTHotelCode <> ''
              AND ota.GeoPoint IS NOT NULL
              AND ota.GeoPoint.STDistance(@origin) <= @recallRadiusMeters
            ORDER BY ota.GeoPoint.STDistance(@origin), ota.HotelID
            OPTION (RECOMPILE, MAXDOP 1);
            """;
        return await QueryOtaRecallRowsAsync(
            commandText,
            [
                new SqlParameter("@latitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(latitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@longitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(longitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@recallRadiusMeters", System.Data.SqlDbType.Int)
                {
                    Value = recallRadiusMeters
                },
                new SqlParameter("@maxCandidates", System.Data.SqlDbType.Int)
                {
                    Value = maxCandidates
                },
                new SqlParameter("@cleanStatus", System.Data.SqlDbType.TinyInt)
                {
                    Value = (byte)HotelCleanStatus.Ready
                },
                new SqlParameter("@dataQualityStatus", System.Data.SqlDbType.TinyInt)
                {
                    Value = (byte)HotelDataQualityStatus.Valid
                },
                new SqlParameter("@onlineEstate", System.Data.SqlDbType.Int)
                {
                    Value = 1
                }
            ],
            cancellationToken);
    }

    async Task<List<OTAHotelInfo>> LoadNearbyOtaHotelsByGridAsync(
        decimal latitude,
        decimal longitude,
        int recallRadiusMeters,
        CancellationToken cancellationToken)
    {
        var parameters = new List<object>
        {
            new SqlParameter("@cleanStatus", System.Data.SqlDbType.TinyInt)
            {
                Value = (byte)HotelCleanStatus.Ready
            },
            new SqlParameter("@dataQualityStatus", System.Data.SqlDbType.TinyInt)
            {
                Value = (byte)HotelDataQualityStatus.Valid
            },
            new SqlParameter("@onlineEstate", System.Data.SqlDbType.Int)
            {
                Value = 1
            }
        };
        var gridPredicate = AddRecallGridParameters(
            "ota.RecallGridId",
            latitude,
            longitude,
            recallRadiusMeters,
            parameters);
        var commandText = """
            SELECT
                ota.HotelID,
                ota.MTHotelCode,
                ota.HotelName,
                ota.HotelEnName,
                ota.Longitude,
                ota.Latitude,
                ota.StarLevel,
                ota.Address,
                ota.RegionLevel1Code,
                ota.CountryCode,
                ota.CountryName,
                ota.CityCode,
                ota.CityName,
                ota.PostCode,
                ota.Tel,
                ota.Fax,
                ota.Email,
                ota.CoordType,
                ota.RegionLevel1Name,
                ota.RegionLevel2Code,
                ota.RegionLevel2Name,
                ota.RegionLevel3Code,
                ota.RegionLevel3Name,
                ota.RegionResolveStatus,
                ota.DataQualityStatus,
                ota.DataQualityFlags
            FROM dbo.OTAHotelInfo AS ota WITH (NOLOCK, INDEX(IX_OTAHotelInfo_RecallGridId))
            WHERE ota.CleanStatus = @cleanStatus
              AND ota.DataQualityStatus = @dataQualityStatus
              AND ota.Estate = @onlineEstate
              AND ota.MTHotelCode <> ''
              AND (
                  /*GRID_PREDICATE*/
              )
            OPTION (RECOMPILE, MAXDOP 1);
            """.Replace(
                "/*GRID_PREDICATE*/",
                gridPredicate,
                StringComparison.Ordinal);
        return await QueryOtaRecallRowsAsync(
            commandText,
            parameters,
            cancellationToken);
    }

    async Task<List<SupHotelInfoClean>> LoadNearbySupplierHotelsAsync(
        int pendingSupHotelId,
        decimal latitude,
        decimal longitude,
        int recallRadiusMeters,
        int? maxCandidates,
        CancellationToken cancellationToken)
    {
        var rows = maxCandidates.HasValue
            ? await LoadNearbySupplierHotelsByDistanceAsync(
                pendingSupHotelId,
                latitude,
                longitude,
                recallRadiusMeters,
                maxCandidates.Value,
                cancellationToken)
            : await LoadNearbySupplierHotelsByGridAsync(
                pendingSupHotelId,
                latitude,
                longitude,
                recallRadiusMeters,
                cancellationToken);
        rows.RemoveAll(hotel =>
            !HotelRecallPolicy.IsMatchingEnabled(hotel.RegionLevel1Code));
        SortByDistance(
            rows,
            latitude,
            longitude,
            hotel => hotel.Latitude,
            hotel => hotel.Longitude,
            hotel => hotel.SupHotelId);
        return rows;
    }

    async Task<List<SupHotelInfoClean>> LoadNearbySupplierHotelsByDistanceAsync(
        int pendingSupHotelId,
        decimal latitude,
        decimal longitude,
        int recallRadiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var commandText = """
            DECLARE @origin geography = geography::Point(@latitude, @longitude, 4326);

            SELECT TOP (@maxCandidates)
                supplier.SupHotelId,
                supplier.SupId,
                supplier.SHotelCode,
                supplier.MTHotelCode,
                supplier.HotelName,
                supplier.HotelNameEn,
                supplier.Longitude,
                supplier.Latitude,
                supplier.StarLevel,
                supplier.Address,
                supplier.RegionLevel1Code,
                supplier.State,
                supplier.CountryCode,
                supplier.CountryName,
                supplier.CityCode,
                supplier.CityName,
                supplier.PostCode,
                supplier.Tel,
                supplier.Fax,
                supplier.Email,
                supplier.CoordType,
                supplier.RegionLevel1Name,
                supplier.RegionLevel2Code,
                supplier.RegionLevel2Name,
                supplier.RegionLevel3Code,
                supplier.RegionLevel3Name,
                supplier.RegionResolveStatus,
                supplier.DataQualityStatus,
                supplier.DataQualityFlags
            FROM dbo.SupHotelInfo AS supplier WITH (NOLOCK, INDEX(SIX_SupHotelInfo_GeoPoint))
            WHERE supplier.State = 1
              AND supplier.SupHotelId <> @pendingSupHotelId
              AND supplier.GeoPoint IS NOT NULL
              AND supplier.GeoPoint.STDistance(@origin) <= @recallRadiusMeters
            ORDER BY supplier.GeoPoint.STDistance(@origin), supplier.SupHotelId
            OPTION (RECOMPILE, MAXDOP 1);
            """;
        return await QuerySupplierRecallRowsAsync(
            commandText,
            [
                new SqlParameter("@pendingSupHotelId", System.Data.SqlDbType.Int)
                {
                    Value = pendingSupHotelId
                },
                new SqlParameter("@latitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(latitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@longitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(longitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@recallRadiusMeters", System.Data.SqlDbType.Int)
                {
                    Value = recallRadiusMeters
                },
                new SqlParameter("@maxCandidates", System.Data.SqlDbType.Int)
                {
                    Value = maxCandidates
                }
            ],
            cancellationToken);
    }

    async Task<List<SupHotelInfoClean>> LoadNearbySupplierHotelsByGridAsync(
        int pendingSupHotelId,
        decimal latitude,
        decimal longitude,
        int recallRadiusMeters,
        CancellationToken cancellationToken)
    {
        var parameters = new List<object>
        {
            new SqlParameter("@pendingSupHotelId", System.Data.SqlDbType.Int)
            {
                Value = pendingSupHotelId
            }
        };
        var gridPredicate = AddRecallGridParameters(
            "supplier.RecallGridId",
            latitude,
            longitude,
            recallRadiusMeters,
            parameters);
        var commandText = """
            SELECT
                supplier.SupHotelId,
                supplier.SupId,
                supplier.SHotelCode,
                supplier.MTHotelCode,
                supplier.HotelName,
                supplier.HotelNameEn,
                supplier.Longitude,
                supplier.Latitude,
                supplier.StarLevel,
                supplier.Address,
                supplier.RegionLevel1Code,
                supplier.State,
                supplier.CountryCode,
                supplier.CountryName,
                supplier.CityCode,
                supplier.CityName,
                supplier.PostCode,
                supplier.Tel,
                supplier.Fax,
                supplier.Email,
                supplier.CoordType,
                supplier.RegionLevel1Name,
                supplier.RegionLevel2Code,
                supplier.RegionLevel2Name,
                supplier.RegionLevel3Code,
                supplier.RegionLevel3Name,
                supplier.RegionResolveStatus,
                supplier.DataQualityStatus,
                supplier.DataQualityFlags
            FROM dbo.SupHotelInfo AS supplier WITH (NOLOCK, INDEX(IX_SupHotelInfo_RecallGridId))
            WHERE supplier.State = 1
              AND supplier.SupHotelId <> @pendingSupHotelId
              AND (
                  /*GRID_PREDICATE*/
              )
            OPTION (RECOMPILE, MAXDOP 1);
            """.Replace(
                "/*GRID_PREDICATE*/",
                gridPredicate,
                StringComparison.Ordinal);
        return await QuerySupplierRecallRowsAsync(
            commandText,
            parameters,
            cancellationToken);
    }

    async Task<List<SupHotelInfoClean>> LoadLinkedSupplierHotelsAsync(
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = """
            SET NOCOUNT ON;

            SELECT
                hotel.SupHotelId,
                hotel.SupId,
                hotel.SHotelCode,
                hotel.MTHotelCode,
                hotel.HotelName,
                hotel.HotelNameEn,
                hotel.Longitude,
                hotel.Latitude,
                hotel.StarLevel,
                hotel.Address,
                hotel.RegionLevel1Code
            FROM OPENJSON(@mtHotelCodes)
                WITH (MTHotelCode varchar(100) '$') AS requested
            INNER LOOP JOIN dbo.SupHotelInfo AS hotel WITH (NOLOCK)
                ON hotel.MTHotelCode = requested.MTHotelCode
            WHERE hotel.State = 1
              AND
              (
                  hotel.RegionLevel1Code IS NULL
                  OR UPPER(LTRIM(RTRIM(hotel.RegionLevel1Code)))
                      NOT IN ('CN', 'CHN', 'TW', 'TWN', 'HK', 'HKG', 'MO', 'MAC')
              )
            OPTION (FORCE ORDER, MAXDOP 1);
            """;
        command.Parameters.Add(new SqlParameter(
            "@mtHotelCodes",
            System.Data.SqlDbType.NVarChar,
            -1)
        {
            Value = JsonSerializer.Serialize(mtHotelCodes, JsonOptions)
        });

        var hotels = new List<SupHotelInfoClean>();
        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hotels.Add(new SupHotelInfoClean
            {
                SupHotelId = reader.GetInt32(0),
                SupId = reader.GetInt32(1),
                SHotelCode = reader.GetString(2),
                MTHotelCode = reader.GetString(3),
                HotelName = reader.IsDBNull(4) ? null : reader.GetString(4),
                HotelNameEn = reader.IsDBNull(5) ? null : reader.GetString(5),
                Longitude = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                Latitude = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                StarLevel = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                Address = reader.IsDBNull(9) ? null : reader.GetString(9),
                RegionLevel1Code = reader.IsDBNull(10) ? null : reader.GetString(10),
                State = 1
            });
        }
        return hotels;
    }

    async Task LoadAndAttachMatchingPhonesAsync(
        IReadOnlyCollection<OTAHotelInfo> otaRows,
        IReadOnlyCollection<SupHotelInfoClean> supplierRows,
        CancellationToken cancellationToken)
    {
        var otaIds = otaRows
            .Select(hotel => hotel.HotelID)
            .Distinct()
            .ToArray();
        var otaPhones = await LoadOtaMatchingPhonesAsync(otaIds, cancellationToken);
        var otaPhonesByHotel = otaPhones.ToLookup(phone => phone.HotelID);
        foreach (var hotel in otaRows)
        {
            hotel.Phones = otaPhonesByHotel[hotel.HotelID].ToArray();
        }

        var distinctSupplierRows = supplierRows
            .GroupBy(hotel => hotel.SupHotelId)
            .Select(group => group.First())
            .ToArray();
        var supplierIds = distinctSupplierRows
            .Select(hotel => hotel.SupHotelId)
            .ToArray();
        var supplierPhones = await LoadSupplierMatchingPhonesAsync(
            supplierIds,
            cancellationToken);
        var supplierPhonesByHotel = supplierPhones.ToLookup(phone => phone.SupHotelId);
        foreach (var hotel in distinctSupplierRows)
        {
            hotel.Phones = supplierPhonesByHotel[hotel.SupHotelId].ToArray();
        }
    }

    async Task<List<OTAHotelPhone>> LoadOtaMatchingPhonesAsync(
        IReadOnlyCollection<int> hotelIds,
        CancellationToken cancellationToken)
    {
        if (hotelIds.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = """
            SET NOCOUNT ON;

            SELECT
                phone.HotelID,
                phone.Ordinal,
                phone.NormalizedPhone
            FROM OPENJSON(@hotelIds)
                WITH (HotelID int '$') AS requested
            INNER LOOP JOIN dbo.OTAHotelPhone AS phone WITH (FORCESEEK)
                ON phone.HotelID = requested.HotelID
            WHERE phone.IsValid = 1
              AND phone.NormalizedPhone IS NOT NULL
            OPTION (FORCE ORDER, RECOMPILE);
            """;
        command.Parameters.Add(new SqlParameter(
            "@hotelIds",
            System.Data.SqlDbType.NVarChar,
            -1)
        {
            Value = JsonSerializer.Serialize(hotelIds, JsonOptions)
        });

        var phones = new List<OTAHotelPhone>();
        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            phones.Add(new OTAHotelPhone
            {
                HotelID = reader.GetInt32(0),
                Ordinal = reader.GetInt16(1),
                NormalizedPhone = reader.GetString(2),
                IsValid = true
            });
        }
        return phones;
    }

    async Task<List<SupHotelPhone>> LoadSupplierMatchingPhonesAsync(
        IReadOnlyCollection<int> supHotelIds,
        CancellationToken cancellationToken)
    {
        if (supHotelIds.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = """
            SET NOCOUNT ON;

            SELECT
                phone.SupHotelId,
                phone.Ordinal,
                phone.E164
            FROM OPENJSON(@supHotelIds)
                WITH (SupHotelId int '$') AS requested
            INNER LOOP JOIN dbo.SupHotelPhone AS phone WITH (FORCESEEK)
                ON phone.SupHotelId = requested.SupHotelId
            WHERE phone.IsValid = 1
              AND phone.E164 IS NOT NULL
            OPTION (FORCE ORDER, RECOMPILE);
            """;
        command.Parameters.Add(new SqlParameter(
            "@supHotelIds",
            System.Data.SqlDbType.NVarChar,
            -1)
        {
            Value = JsonSerializer.Serialize(supHotelIds, JsonOptions)
        });

        var phones = new List<SupHotelPhone>();
        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            phones.Add(new SupHotelPhone
            {
                SupHotelId = reader.GetInt32(0),
                Ordinal = reader.GetInt16(1),
                E164 = reader.GetString(2),
                IsValid = true
            });
        }
        return phones;
    }

    public async Task SaveMatchEvaluationsAsync(
        IReadOnlyCollection<HotelMatchEvaluation> evaluations,
        int matchingVersion,
        CancellationToken cancellationToken)
    {
        if (evaluations.Count == 0)
        {
            return;
        }

        var evaluationRows = evaluations.Select(evaluation => new
        {
            supHotelId = evaluation.SupHotelId,
            supId = evaluation.SupId,
            supHotelCode = evaluation.SupHotelCode,
            matchCount = evaluation.Matches.Count,
            matchingVersion,
            mtHotelCodesJson = JsonSerializer.Serialize(
                evaluation.Matches.Select(match => match.MTHotelCode),
                JsonOptions),
            candidatesJson = JsonSerializer.Serialize(evaluation.Matches, JsonOptions)
        }).ToArray();
        var matchRows = evaluations.Select(evaluation =>
            !string.IsNullOrEmpty(evaluation.ResolvedMTHotelCode)
                ? CreateMatchResultRow(
                    evaluation.SupHotelId,
                    evaluation.Matches[0],
                    matchingVersion)
                : CreateUnmatchedResultRow(evaluation, matchingVersion))
            .ToArray();
        var evaluationsParameter = new SqlParameter("@evaluations", System.Data.SqlDbType.NVarChar, -1)
        {
            Value = JsonSerializer.Serialize(evaluationRows, JsonOptions)
        };
        var matchesParameter = new SqlParameter("@matches", System.Data.SqlDbType.NVarChar, -1)
        {
            Value = JsonSerializer.Serialize(matchRows, JsonOptions)
        };
        var nowParameter = new SqlParameter("@now", System.Data.SqlDbType.DateTime2)
        {
            Value = DateTime.Now
        };
        var matchingVersionParameter = new SqlParameter(
            "@matchingVersion",
            System.Data.SqlDbType.Int)
        {
            Value = matchingVersion
        };

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!_hotelMatchResultHasLegacyMatchingVersion.HasValue)
        {
            await using var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = """
                SELECT CONVERT(bit, CASE
                    WHEN COL_LENGTH(N'dbo.HotelMatchResult', N'MatchingVersion') IS NULL THEN 0
                    ELSE 1
                END);
                """;
            _hotelMatchResultHasLegacyMatchingVersion =
                (bool)(await schemaCommand.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "Unable to inspect dbo.HotelMatchResult schema."));
            if (_hotelMatchResultHasLegacyMatchingVersion.Value)
            {
                _logger.LogWarning(
                    "dbo.HotelMatchResult still contains legacy MatchingVersion. " +
                    "Writes will remain compatible, but HotelMatchResult.sql should be applied.");
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        var commandText = """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            BEGIN TRY
                DELETE older
                FROM dbo.HotelMatchResult AS older WITH (ROWLOCK)
                INNER JOIN OPENJSON(@evaluations)
                    WITH (SupHotelId int '$.supHotelId') AS evaluation
                    ON evaluation.SupHotelId = older.SupHotelId
                WHERE EXISTS
                (
                    SELECT 1
                    FROM dbo.HotelMatchResult AS newer
                    WHERE newer.SupHotelId = older.SupHotelId
                      AND newer.Id > older.Id
                );

                MERGE dbo.HotelMatchResult WITH (HOLDLOCK, ROWLOCK) AS target
                USING
                (
                    SELECT *
                    FROM OPENJSON(@matches)
                    WITH
                    (
                        SupHotelId int '$.supHotelId',
                        MTHotelCode varchar(100) '$.mtHotelCode',
                        /*LEGACY_MATCHING_VERSION_SOURCE*/
                        NameEvidenceSourceType tinyint '$.nameEvidenceSourceType',
                        NameEvidenceHotelId int '$.nameEvidenceHotelId',
                        NameMatchKind tinyint '$.nameMatchKind',
                        NamePendingValue nvarchar(300) '$.namePendingValue',
                        NameEvidenceValue nvarchar(300) '$.nameEvidenceValue',
                        CoordinateEvidenceSourceType tinyint '$.coordinateEvidenceSourceType',
                        CoordinateEvidenceHotelId int '$.coordinateEvidenceHotelId',
                        DistanceMeters decimal(12,3) '$.distanceMeters',
                        StarEvidenceSourceType tinyint '$.starEvidenceSourceType',
                        StarEvidenceHotelId int '$.starEvidenceHotelId',
                        StarPendingValue varchar(20) '$.starPendingValue',
                        StarEvidenceValue varchar(20) '$.starEvidenceValue',
                        AddressEvidenceSourceType tinyint '$.addressEvidenceSourceType',
                        AddressEvidenceHotelId int '$.addressEvidenceHotelId',
                        AddressPendingValue nvarchar(500) '$.addressPendingValue',
                        AddressEvidenceValue nvarchar(500) '$.addressEvidenceValue',
                        PhoneEvidenceSourceType tinyint '$.phoneEvidenceSourceType',
                        PhoneEvidenceHotelId int '$.phoneEvidenceHotelId',
                        PhonePendingValue varchar(32) '$.phonePendingValue',
                        PhoneEvidenceValue varchar(32) '$.phoneEvidenceValue',
                        OtaFactCount int '$.otaFactCount',
                        SupplierFactCount int '$.supplierFactCount',
                        EvidenceJson nvarchar(max) '$.evidenceJson'
                    )
                ) AS source
                    ON source.SupHotelId = target.SupHotelId
                WHEN MATCHED THEN UPDATE SET
                    target.MTHotelCode = source.MTHotelCode,
                    target.NameEvidenceSourceType = source.NameEvidenceSourceType,
                    target.NameEvidenceHotelId = source.NameEvidenceHotelId,
                    target.NameMatchKind = source.NameMatchKind,
                    target.NamePendingValue = source.NamePendingValue,
                    target.NameEvidenceValue = source.NameEvidenceValue,
                    target.CoordinateEvidenceSourceType = source.CoordinateEvidenceSourceType,
                    target.CoordinateEvidenceHotelId = source.CoordinateEvidenceHotelId,
                    target.DistanceMeters = source.DistanceMeters,
                    target.StarEvidenceSourceType = source.StarEvidenceSourceType,
                    target.StarEvidenceHotelId = source.StarEvidenceHotelId,
                    target.StarPendingValue = source.StarPendingValue,
                    target.StarEvidenceValue = source.StarEvidenceValue,
                    target.AddressEvidenceSourceType = source.AddressEvidenceSourceType,
                    target.AddressEvidenceHotelId = source.AddressEvidenceHotelId,
                    target.AddressPendingValue = source.AddressPendingValue,
                    target.AddressEvidenceValue = source.AddressEvidenceValue,
                    target.PhoneEvidenceSourceType = source.PhoneEvidenceSourceType,
                    target.PhoneEvidenceHotelId = source.PhoneEvidenceHotelId,
                    target.PhonePendingValue = source.PhonePendingValue,
                    target.PhoneEvidenceValue = source.PhoneEvidenceValue,
                    target.OtaFactCount = source.OtaFactCount,
                    target.SupplierFactCount = source.SupplierFactCount,
                    /*LEGACY_MATCHING_VERSION_UPDATE*/
                    target.EvidenceJson = source.EvidenceJson,
                    target.UpdateTime = @now
                WHEN NOT MATCHED THEN INSERT
                (
                    SupHotelId, MTHotelCode,
                    /*LEGACY_MATCHING_VERSION_INSERT_COLUMN*/
                    NameEvidenceSourceType, NameEvidenceHotelId, NameMatchKind,
                    NamePendingValue, NameEvidenceValue,
                    CoordinateEvidenceSourceType, CoordinateEvidenceHotelId, DistanceMeters,
                    StarEvidenceSourceType, StarEvidenceHotelId, StarPendingValue, StarEvidenceValue,
                    AddressEvidenceSourceType, AddressEvidenceHotelId,
                    AddressPendingValue, AddressEvidenceValue,
                    PhoneEvidenceSourceType, PhoneEvidenceHotelId,
                    PhonePendingValue, PhoneEvidenceValue,
                    OtaFactCount, SupplierFactCount, EvidenceJson,
                    CreateTime, UpdateTime
                )
                VALUES
                (
                    source.SupHotelId, source.MTHotelCode,
                    /*LEGACY_MATCHING_VERSION_INSERT_VALUE*/
                    source.NameEvidenceSourceType, source.NameEvidenceHotelId, source.NameMatchKind,
                    source.NamePendingValue, source.NameEvidenceValue,
                    source.CoordinateEvidenceSourceType, source.CoordinateEvidenceHotelId,
                    source.DistanceMeters,
                    source.StarEvidenceSourceType, source.StarEvidenceHotelId,
                    source.StarPendingValue, source.StarEvidenceValue,
                    source.AddressEvidenceSourceType, source.AddressEvidenceHotelId,
                    source.AddressPendingValue, source.AddressEvidenceValue,
                    source.PhoneEvidenceSourceType, source.PhoneEvidenceHotelId,
                    source.PhonePendingValue, source.PhoneEvidenceValue,
                    source.OtaFactCount, source.SupplierFactCount, source.EvidenceJson,
                    @now, @now
                )
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                MERGE dbo.HotelMatchConflict WITH (HOLDLOCK, ROWLOCK) AS target
                USING
                (
                    SELECT *
                    FROM OPENJSON(@evaluations)
                    WITH
                    (
                        SupHotelId int '$.supHotelId',
                        SupId int '$.supId',
                        SupHotelCode nvarchar(50) '$.supHotelCode',
                        MatchCount int '$.matchCount',
                        MatchingVersion int '$.matchingVersion',
                        MTHotelCodesJson nvarchar(max) '$.mtHotelCodesJson',
                        CandidatesJson nvarchar(max) '$.candidatesJson'
                    )
                ) AS source
                    ON source.SupHotelId = target.SupHotelId
                WHEN MATCHED THEN UPDATE SET
                    target.SupId = source.SupId,
                    target.SupHotelCode = source.SupHotelCode,
                    target.MatchingVersion = source.MatchingVersion,
                    target.CandidateCount = CASE
                        WHEN source.MatchCount > 1 THEN source.MatchCount
                        ELSE target.CandidateCount
                    END,
                    target.MTHotelCodesJson = CASE
                        WHEN source.MatchCount > 1 THEN source.MTHotelCodesJson
                        ELSE target.MTHotelCodesJson
                    END,
                    target.CandidatesJson = CASE
                        WHEN source.MatchCount > 1 THEN source.CandidatesJson
                        ELSE target.CandidatesJson
                    END,
                    target.IsActive = CASE
                        WHEN source.MatchCount > 1 THEN 1
                        ELSE 0
                    END,
                    target.LastConflictTime = CASE
                        WHEN source.MatchCount > 1 THEN @now
                        ELSE target.LastConflictTime
                    END,
                    target.ResolvedTime = CASE
                        WHEN source.MatchCount > 1 THEN NULL
                        WHEN target.IsActive = 1 THEN @now
                        ELSE target.ResolvedTime
                    END,
                    target.UpdateTime = @now
                WHEN NOT MATCHED AND source.MatchCount > 1 THEN INSERT
                (
                    SupHotelId, SupId, SupHotelCode, MatchingVersion,
                    CandidateCount, MTHotelCodesJson, CandidatesJson,
                    IsActive, FirstConflictTime, LastConflictTime,
                    CreateTime, UpdateTime
                )
                VALUES
                (
                    source.SupHotelId, source.SupId, source.SupHotelCode,
                    source.MatchingVersion, source.MatchCount,
                    source.MTHotelCodesJson, source.CandidatesJson,
                    1, @now, @now, @now, @now
                )
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                UPDATE target WITH (ROWLOCK)
                SET
                    target.LastMatchTime = @now,
                    target.NextMatchTime = NULL,
                    target.MatchAttemptCount = target.MatchAttemptCount + 1,
                    target.MatchError = NULL,
                    target.MatchStatus = CASE
                        WHEN source.MatchCount = 0 THEN 2
                        WHEN source.MatchCount = 1 THEN 4
                        ELSE 3
                    END,
                    target.UpdateTime = @now
                FROM dbo.SupHotelInfo AS target
                INNER JOIN OPENJSON(@evaluations)
                    WITH
                    (
                        SupHotelId int '$.supHotelId',
                        MatchCount int '$.matchCount'
                    ) AS source
                    ON source.SupHotelId = target.SupHotelId
                OPTION (LOOP JOIN, MAXDOP 1, RECOMPILE);

                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """;
        command.CommandText = _hotelMatchResultHasLegacyMatchingVersion.Value
            ? commandText
                .Replace(
                    "/*LEGACY_MATCHING_VERSION_SOURCE*/",
                    "MatchingVersion int '$.matchingVersion',",
                    StringComparison.Ordinal)
                .Replace(
                    "/*LEGACY_MATCHING_VERSION_UPDATE*/",
                    "target.MatchingVersion = source.MatchingVersion,",
                    StringComparison.Ordinal)
                .Replace(
                    "/*LEGACY_MATCHING_VERSION_INSERT_COLUMN*/",
                    "MatchingVersion,",
                    StringComparison.Ordinal)
                .Replace(
                    "/*LEGACY_MATCHING_VERSION_INSERT_VALUE*/",
                    "source.MatchingVersion,",
                    StringComparison.Ordinal)
            : commandText
                .Replace("/*LEGACY_MATCHING_VERSION_SOURCE*/", string.Empty, StringComparison.Ordinal)
                .Replace("/*LEGACY_MATCHING_VERSION_UPDATE*/", string.Empty, StringComparison.Ordinal)
                .Replace("/*LEGACY_MATCHING_VERSION_INSERT_COLUMN*/", string.Empty, StringComparison.Ordinal)
                .Replace("/*LEGACY_MATCHING_VERSION_INSERT_VALUE*/", string.Empty, StringComparison.Ordinal);
        command.Parameters.AddRange(
            [evaluationsParameter, matchesParameter, nowParameter, matchingVersionParameter]);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    static HotelMatchResultWriteRow CreateUnmatchedResultRow(
        HotelMatchEvaluation evaluation,
        int matchingVersion)
        => new(
            evaluation.SupHotelId,
            string.Empty,
            matchingVersion,
            0,
            0,
            0,
            null,
            null,
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            0,
            JsonSerializer.Serialize(
                new
                {
                    status = evaluation.Matches.Count == 0 ? "noMatch" : "conflict",
                    candidateCount = evaluation.Matches.Count,
                    mtHotelCodes = evaluation.Matches
                        .Select(match => match.MTHotelCode)
                        .ToArray()
                },
                JsonOptions));

    static HotelMatchResultWriteRow CreateMatchResultRow(
        int supHotelId,
        HotelMatchedCandidate match,
        int matchingVersion)
        => new(
            supHotelId,
            match.MTHotelCode,
            matchingVersion,
            (byte)match.NameEvidence.Source,
            match.NameEvidence.HotelId,
            (byte)match.NameMatchKind,
            match.NameEvidence.PendingValue,
            match.NameEvidence.EvidenceValue,
            (byte)match.CoordinateEvidence.Source,
            match.CoordinateEvidence.HotelId,
            match.CoordinateEvidence.DistanceMeters ?? 0,
            match.StarEvidence is null ? null : (byte)match.StarEvidence.Source,
            match.StarEvidence?.HotelId,
            match.StarEvidence?.PendingValue,
            match.StarEvidence?.EvidenceValue,
            match.AddressEvidence is null ? null : (byte)match.AddressEvidence.Source,
            match.AddressEvidence?.HotelId,
            match.AddressEvidence?.PendingValue,
            match.AddressEvidence?.EvidenceValue,
            match.PhoneEvidence is null ? null : (byte)match.PhoneEvidence.Source,
            match.PhoneEvidence?.HotelId,
            match.PhoneEvidence?.PendingValue,
            match.PhoneEvidence?.EvidenceValue,
            match.OtaFactCount,
            match.SupplierFactCount,
            JsonSerializer.Serialize(match, JsonOptions));

    public async Task<IReadOnlyList<HotelMatchWorkItem>> ClaimUnmatchedSupplierHotelsAsync(
        int matchingVersion,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var activeDeliveryStates = new[] { DeliveryPending, DeliverySending, DeliveryDelivered };
        var matchingDisabledCountryCodes = new[]
        {
            "CN", "CHN", "TW", "TWN", "HK", "HKG", "MO", "MAC"
        };
        var now = DateTime.Now;
        var rows = await _dbContext.SupHotelInfos.AsTracking()
            .Include(x => x.Phones)
            .Where(x => x.CleanStatus == (byte)HotelCleanStatus.Ready
                && x.DataQualityStatus == (byte)HotelDataQualityStatus.Valid
                && x.Longitude.HasValue
                && x.Latitude.HasValue
                && string.IsNullOrEmpty(x.MTHotelCode)
                && !matchingDisabledCountryCodes.Contains(
                    (x.RegionLevel1Code ?? string.Empty).Trim())
                && !_dbContext.HotelMatchDeliveries.Any(delivery =>
                    delivery.SupId == x.SupId
                    && delivery.SHotelCode == x.SHotelCode
                    && activeDeliveryStates.Contains(delivery.DeliveryStatus))
                && (x.MatchStatus == (byte)HotelMatchStatus.Pending
                    || x.LastMatchVersion != matchingVersion
                    || x.LastMatchedDataHash != x.NormalizedDataHash
                    || x.ReMatchState == 1
                    || x.MatchStatus == (byte)HotelMatchStatus.Failed && x.NextMatchTime <= now))
            .OrderBy(x => x.LastMatchTime)
            .ThenBy(x => x.SupHotelId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.MatchStatus = (byte)HotelMatchStatus.Processing;
            row.MatchAttemptCount++;
            row.UpdateTime = now;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        return rows.Select(MapWorkItem).ToArray();
    }

    public async Task<IReadOnlyList<CleanedHotel>> RecallOtaCandidatesAsync(
        HotelMatchWorkItem supplier,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (!HotelRecallPolicy.IsMatchingEnabled(
                supplier.Hotel.RegionLevel1Code))
        {
            return [];
        }

        var longitude = supplier.Hotel.Longitude ?? throw new InvalidOperationException("Supplier longitude is missing.");
        var latitude = supplier.Hotel.Latitude ?? throw new InvalidOperationException("Supplier latitude is missing.");
        var recall = HotelRecallPolicy.Resolve(
            supplier.Hotel.RegionLevel1Code,
            supplier.Hotel.CountryCode,
            radiusMeters,
            maxCandidates);
        var rows = recall.MaxCandidates.HasValue
            ? await RecallOtaCandidatesByDistanceAsync(
                latitude,
                longitude,
                recall.RadiusMeters,
                recall.MaxCandidates.Value,
                cancellationToken)
            : await RecallOtaCandidatesByGridAsync(
                latitude,
                longitude,
                recall.RadiusMeters,
                cancellationToken);
        rows.RemoveAll(hotel =>
            !HotelRecallPolicy.IsMatchingEnabled(hotel.RegionLevel1Code));
        SortByDistance(
            rows,
            latitude,
            longitude,
            hotel => hotel.Latitude,
            hotel => hotel.Longitude,
            hotel => hotel.HotelID);
        await LoadAndAttachMatchingPhonesAsync(rows, [], cancellationToken);
        return rows.Select(MapCleaned).ToArray();
    }

    public async Task<IReadOnlyList<CleanedHotel>> RecallSupplierCandidatesAsync(
        CleanedHotel ota,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (!HotelRecallPolicy.IsMatchingEnabled(ota.RegionLevel1Code))
        {
            return [];
        }

        var longitude = ota.Longitude ?? throw new InvalidOperationException("OTA longitude is missing.");
        var latitude = ota.Latitude ?? throw new InvalidOperationException("OTA latitude is missing.");
        var recall = HotelRecallPolicy.Resolve(
            ota.RegionLevel1Code,
            ota.CountryCode,
            radiusMeters,
            maxCandidates);
        var rows = recall.MaxCandidates.HasValue
            ? await RecallSupplierCandidatesByDistanceAsync(
                latitude,
                longitude,
                recall.RadiusMeters,
                recall.MaxCandidates.Value,
                cancellationToken)
            : await RecallSupplierCandidatesByGridAsync(
                latitude,
                longitude,
                recall.RadiusMeters,
                cancellationToken);
        rows.RemoveAll(hotel =>
            !HotelRecallPolicy.IsMatchingEnabled(hotel.RegionLevel1Code));
        SortByDistance(
            rows,
            latitude,
            longitude,
            hotel => hotel.Latitude,
            hotel => hotel.Longitude,
            hotel => hotel.SupHotelId);
        await LoadAndAttachMatchingPhonesAsync([], rows, cancellationToken);
        return rows.Select(MapCleaned).ToArray();
    }

    async Task<List<OTAHotelInfo>> RecallOtaCandidatesByDistanceAsync(
        decimal latitude,
        decimal longitude,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        const string commandText = """
            DECLARE @origin geography = geography::Point(@latitude, @longitude, 4326);

            SELECT TOP (@maxCandidates)
                o.HotelID,
                o.MTHotelCode,
                o.HotelName,
                o.HotelEnName,
                o.Longitude,
                o.Latitude,
                o.StarLevel,
                o.Address,
                o.RegionLevel1Code,
                o.CountryCode,
                o.CountryName,
                o.CityCode,
                o.CityName,
                o.PostCode,
                o.Tel,
                o.Fax,
                o.Email,
                o.CoordType,
                o.RegionLevel1Name,
                o.RegionLevel2Code,
                o.RegionLevel2Name,
                o.RegionLevel3Code,
                o.RegionLevel3Name,
                o.RegionResolveStatus,
                o.DataQualityStatus,
                o.DataQualityFlags
            FROM dbo.OTAHotelInfo AS o WITH (NOLOCK, INDEX(SIX_OTAHotelInfo_GeoPoint))
            WHERE o.CleanStatus = @cleanStatus
              AND o.DataQualityStatus = @dataQualityStatus
              AND o.Estate = @onlineEstate
              AND o.GeoPoint IS NOT NULL
              AND o.GeoPoint.STDistance(@origin) <= @radiusMeters
            ORDER BY o.GeoPoint.STDistance(@origin), o.HotelID
            OPTION (RECOMPILE, MAXDOP 1);
            """;
        return await QueryOtaRecallRowsAsync(
            commandText,
            [
                new SqlParameter("@latitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(latitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@longitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(longitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@radiusMeters", System.Data.SqlDbType.Int)
                {
                    Value = radiusMeters
                },
                new SqlParameter("@maxCandidates", System.Data.SqlDbType.Int)
                {
                    Value = maxCandidates
                },
                new SqlParameter("@cleanStatus", System.Data.SqlDbType.TinyInt)
                {
                    Value = (byte)HotelCleanStatus.Ready
                },
                new SqlParameter("@dataQualityStatus", System.Data.SqlDbType.TinyInt)
                {
                    Value = (byte)HotelDataQualityStatus.Valid
                },
                new SqlParameter("@onlineEstate", System.Data.SqlDbType.Int)
                {
                    Value = 1
                }
            ],
            cancellationToken);
    }

    async Task<List<OTAHotelInfo>> RecallOtaCandidatesByGridAsync(
        decimal latitude,
        decimal longitude,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        var parameters = new List<object>
        {
            new SqlParameter("@cleanStatus", System.Data.SqlDbType.TinyInt)
            {
                Value = (byte)HotelCleanStatus.Ready
            },
            new SqlParameter("@dataQualityStatus", System.Data.SqlDbType.TinyInt)
            {
                Value = (byte)HotelDataQualityStatus.Valid
            },
            new SqlParameter("@onlineEstate", System.Data.SqlDbType.Int)
            {
                Value = 1
            }
        };
        var gridPredicate = AddRecallGridParameters(
            "o.RecallGridId",
            latitude,
            longitude,
            radiusMeters,
            parameters);
        var commandText = """
            SELECT
                o.HotelID,
                o.MTHotelCode,
                o.HotelName,
                o.HotelEnName,
                o.Longitude,
                o.Latitude,
                o.StarLevel,
                o.Address,
                o.RegionLevel1Code,
                o.CountryCode,
                o.CountryName,
                o.CityCode,
                o.CityName,
                o.PostCode,
                o.Tel,
                o.Fax,
                o.Email,
                o.CoordType,
                o.RegionLevel1Name,
                o.RegionLevel2Code,
                o.RegionLevel2Name,
                o.RegionLevel3Code,
                o.RegionLevel3Name,
                o.RegionResolveStatus,
                o.DataQualityStatus,
                o.DataQualityFlags
            FROM dbo.OTAHotelInfo AS o WITH (NOLOCK, INDEX(IX_OTAHotelInfo_RecallGridId))
            WHERE o.CleanStatus = @cleanStatus
              AND o.DataQualityStatus = @dataQualityStatus
              AND o.Estate = @onlineEstate
              AND (
                  /*GRID_PREDICATE*/
              )
            OPTION (RECOMPILE, MAXDOP 1);
            """.Replace(
                "/*GRID_PREDICATE*/",
                gridPredicate,
                StringComparison.Ordinal);
        return await QueryOtaRecallRowsAsync(
            commandText,
            parameters,
            cancellationToken);
    }

    async Task<List<SupHotelInfoClean>> RecallSupplierCandidatesByDistanceAsync(
        decimal latitude,
        decimal longitude,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        const string commandText = """
            DECLARE @origin geography = geography::Point(@latitude, @longitude, 4326);

            SELECT TOP (@maxCandidates)
                s.SupHotelId,
                s.SupId,
                s.SHotelCode,
                s.MTHotelCode,
                s.HotelName,
                s.HotelNameEn,
                s.Longitude,
                s.Latitude,
                s.StarLevel,
                s.Address,
                s.RegionLevel1Code,
                s.State,
                s.CountryCode,
                s.CountryName,
                s.CityCode,
                s.CityName,
                s.PostCode,
                s.Tel,
                s.Fax,
                s.Email,
                s.CoordType,
                s.RegionLevel1Name,
                s.RegionLevel2Code,
                s.RegionLevel2Name,
                s.RegionLevel3Code,
                s.RegionLevel3Name,
                s.RegionResolveStatus,
                s.DataQualityStatus,
                s.DataQualityFlags
            FROM dbo.SupHotelInfo AS s WITH (NOLOCK, INDEX(SIX_SupHotelInfo_GeoPoint))
            WHERE s.State = 1
              AND s.CleanStatus = @cleanStatus
              AND s.DataQualityStatus = @dataQualityStatus
              AND s.GeoPoint IS NOT NULL
              AND s.GeoPoint.STDistance(@origin) <= @radiusMeters
            ORDER BY s.GeoPoint.STDistance(@origin), s.SupHotelId
            OPTION (RECOMPILE, MAXDOP 1);
            """;
        return await QuerySupplierRecallRowsAsync(
            commandText,
            [
                new SqlParameter("@latitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(latitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@longitude", System.Data.SqlDbType.Float)
                {
                    Value = Convert.ToDouble(longitude, CultureInfo.InvariantCulture)
                },
                new SqlParameter("@radiusMeters", System.Data.SqlDbType.Int)
                {
                    Value = radiusMeters
                },
                new SqlParameter("@maxCandidates", System.Data.SqlDbType.Int)
                {
                    Value = maxCandidates
                },
                new SqlParameter("@cleanStatus", System.Data.SqlDbType.TinyInt)
                {
                    Value = (byte)HotelCleanStatus.Ready
                },
                new SqlParameter("@dataQualityStatus", System.Data.SqlDbType.TinyInt)
                {
                    Value = (byte)HotelDataQualityStatus.Valid
                }
            ],
            cancellationToken);
    }

    async Task<List<SupHotelInfoClean>> RecallSupplierCandidatesByGridAsync(
        decimal latitude,
        decimal longitude,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        var parameters = new List<object>
        {
            new SqlParameter("@cleanStatus", System.Data.SqlDbType.TinyInt)
            {
                Value = (byte)HotelCleanStatus.Ready
            },
            new SqlParameter("@dataQualityStatus", System.Data.SqlDbType.TinyInt)
            {
                Value = (byte)HotelDataQualityStatus.Valid
            }
        };
        var gridPredicate = AddRecallGridParameters(
            "s.RecallGridId",
            latitude,
            longitude,
            radiusMeters,
            parameters);
        var commandText = """
            SELECT
                s.SupHotelId,
                s.SupId,
                s.SHotelCode,
                s.MTHotelCode,
                s.HotelName,
                s.HotelNameEn,
                s.Longitude,
                s.Latitude,
                s.StarLevel,
                s.Address,
                s.RegionLevel1Code,
                s.State,
                s.CountryCode,
                s.CountryName,
                s.CityCode,
                s.CityName,
                s.PostCode,
                s.Tel,
                s.Fax,
                s.Email,
                s.CoordType,
                s.RegionLevel1Name,
                s.RegionLevel2Code,
                s.RegionLevel2Name,
                s.RegionLevel3Code,
                s.RegionLevel3Name,
                s.RegionResolveStatus,
                s.DataQualityStatus,
                s.DataQualityFlags
            FROM dbo.SupHotelInfo AS s WITH (NOLOCK, INDEX(IX_SupHotelInfo_RecallGridId))
            WHERE s.State = 1
              AND s.CleanStatus = @cleanStatus
              AND s.DataQualityStatus = @dataQualityStatus
              AND (
                  /*GRID_PREDICATE*/
              )
            OPTION (RECOMPILE, MAXDOP 1);
            """.Replace(
                "/*GRID_PREDICATE*/",
                gridPredicate,
                StringComparison.Ordinal);
        return await QuerySupplierRecallRowsAsync(
            commandText,
            parameters,
            cancellationToken);
    }

    async Task<List<OTAHotelInfo>> QueryOtaRecallRowsAsync(
        string commandText,
        IReadOnlyCollection<object> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var hotels = new List<OTAHotelInfo>();
        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hotels.Add(new OTAHotelInfo
            {
                HotelID = reader.GetInt32(0),
                MTHotelCode = reader.GetString(1),
                HotelName = reader.GetString(2),
                HotelEnName = GetNullableString(reader, 3),
                Longitude = GetNullableDecimal(reader, 4),
                Latitude = GetNullableDecimal(reader, 5),
                StarLevel = GetNullableDecimal(reader, 6),
                Address = GetNullableString(reader, 7),
                RegionLevel1Code = GetNullableString(reader, 8),
                CountryCode = GetNullableString(reader, 9),
                CountryName = GetNullableString(reader, 10),
                CityCode = GetNullableString(reader, 11),
                CityName = GetNullableString(reader, 12),
                PostCode = GetNullableString(reader, 13),
                Tel = GetNullableString(reader, 14),
                Fax = GetNullableString(reader, 15),
                Email = GetNullableString(reader, 16),
                CoordType = reader.GetByte(17),
                RegionLevel1Name = GetNullableString(reader, 18),
                RegionLevel2Code = GetNullableString(reader, 19),
                RegionLevel2Name = GetNullableString(reader, 20),
                RegionLevel3Code = GetNullableString(reader, 21),
                RegionLevel3Name = GetNullableString(reader, 22),
                RegionResolveStatus = reader.GetByte(23),
                DataQualityStatus = reader.GetByte(24),
                DataQualityFlags = reader.GetInt64(25)
            });
        }
        return hotels;
    }

    async Task<List<SupHotelInfoClean>> QuerySupplierRecallRowsAsync(
        string commandText,
        IReadOnlyCollection<object> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var hotels = new List<SupHotelInfoClean>();
        await using var reader = await command.ExecuteReaderAsync(
            System.Data.CommandBehavior.SequentialAccess,
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hotels.Add(new SupHotelInfoClean
            {
                SupHotelId = reader.GetInt32(0),
                SupId = reader.GetInt32(1),
                SHotelCode = reader.GetString(2),
                MTHotelCode = reader.GetString(3),
                HotelName = GetNullableString(reader, 4),
                HotelNameEn = GetNullableString(reader, 5),
                Longitude = GetNullableDecimal(reader, 6),
                Latitude = GetNullableDecimal(reader, 7),
                StarLevel = GetNullableDecimal(reader, 8),
                Address = GetNullableString(reader, 9),
                RegionLevel1Code = GetNullableString(reader, 10),
                State = reader.GetInt32(11),
                CountryCode = GetNullableString(reader, 12),
                CountryName = GetNullableString(reader, 13),
                CityCode = GetNullableString(reader, 14),
                CityName = GetNullableString(reader, 15),
                PostCode = GetNullableString(reader, 16),
                Tel = GetNullableString(reader, 17),
                Fax = GetNullableString(reader, 18),
                Email = GetNullableString(reader, 19),
                CoordType = reader.GetByte(20),
                RegionLevel1Name = GetNullableString(reader, 21),
                RegionLevel2Code = GetNullableString(reader, 22),
                RegionLevel2Name = GetNullableString(reader, 23),
                RegionLevel3Code = GetNullableString(reader, 24),
                RegionLevel3Name = GetNullableString(reader, 25),
                RegionResolveStatus = reader.GetByte(26),
                DataQualityStatus = reader.GetByte(27),
                DataQualityFlags = reader.GetInt64(28)
            });
        }
        return hotels;
    }

    static string AddRecallGridParameters(
        string columnExpression,
        decimal latitude,
        decimal longitude,
        int radiusMeters,
        ICollection<object> parameters)
    {
        var ranges = HotelRecallGrid.GetRanges(latitude, longitude, radiusMeters);
        var predicates = new string[ranges.Count];
        for (var index = 0; index < ranges.Count; index++)
        {
            var minimumParameterName = $"@recallGridMinimum{index}";
            var maximumParameterName = $"@recallGridMaximum{index}";
            predicates[index] =
                $"({columnExpression} BETWEEN {minimumParameterName} AND {maximumParameterName})";
            parameters.Add(new SqlParameter(
                minimumParameterName,
                System.Data.SqlDbType.BigInt)
            {
                Value = ranges[index].Minimum
            });
            parameters.Add(new SqlParameter(
                maximumParameterName,
                System.Data.SqlDbType.BigInt)
            {
                Value = ranges[index].Maximum
            });
        }

        return string.Join(
            $"{Environment.NewLine}                  OR ",
            predicates);
    }

    static async Task<bool> DelayOrCanceledAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await Task.Delay(delay);
            return true;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var cancellationCompletion =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.UnsafeRegister(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            cancellationCompletion);
        var delayTask = Task.Delay(delay);
        return await Task.WhenAny(delayTask, cancellationCompletion.Task) == delayTask;
    }

    static string? GetNullableString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    static void SortByDistance<T>(
        List<T> rows,
        decimal originLatitude,
        decimal originLongitude,
        Func<T, decimal?> latitude,
        Func<T, decimal?> longitude,
        Func<T, int> id)
    {
        rows.Sort((left, right) =>
        {
            var distanceComparison = DistanceMeters(
                    originLatitude,
                    originLongitude,
                    latitude(left),
                    longitude(left))
                .CompareTo(DistanceMeters(
                    originLatitude,
                    originLongitude,
                    latitude(right),
                    longitude(right)));
            return distanceComparison != 0
                ? distanceComparison
                : id(left).CompareTo(id(right));
        });
    }

    static double DistanceMeters(
        decimal originLatitude,
        decimal originLongitude,
        decimal? candidateLatitude,
        decimal? candidateLongitude)
    {
        if (!candidateLatitude.HasValue || !candidateLongitude.HasValue)
        {
            return double.PositiveInfinity;
        }

        const double earthRadiusMeters = 6_371_008.8;
        var latitude1 = (double)originLatitude * Math.PI / 180;
        var latitude2 = (double)candidateLatitude.Value * Math.PI / 180;
        var latitudeDelta = latitude2 - latitude1;
        var longitudeDelta =
            ((double)candidateLongitude.Value - (double)originLongitude)
            * Math.PI
            / 180;
        var haversine = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(latitude1)
            * Math.Cos(latitude2)
            * Math.Sin(longitudeDelta / 2)
            * Math.Sin(longitudeDelta / 2);
        var boundedHaversine = Math.Clamp(haversine, 0, 1);
        return earthRadiusMeters
            * 2
            * Math.Atan2(
                Math.Sqrt(boundedHaversine),
                Math.Sqrt(1 - boundedHaversine));
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetTrustedAliasesAsync(
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken)
    {
        var codes = mtHotelCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (codes.Length == 0)
        {
            return new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await _dbContext.SupHotelInfos.AsNoTracking()
            .Where(x => codes.Contains(x.MTHotelCode)
                && x.DataQualityStatus == (byte)HotelDataQualityStatus.Valid
                && (!string.IsNullOrEmpty(x.HotelName) || !string.IsNullOrEmpty(x.HotelNameEn)))
            .Select(x => new { x.MTHotelCode, x.HotelName, x.HotelNameEn })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.MTHotelCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<string>)x
                    .SelectMany(y => new[] { y.HotelName, y.HotelNameEn })
                    .Where(y => !string.IsNullOrWhiteSpace(y))
                    .Select(y => y!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task CompleteMatchAsync(
        HotelMatchWorkItem workItem,
        HotelMatchDecision decision,
        int matchingVersion,
        string? error,
        DateTimeOffset? nextMatchTime,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.SupHotelInfos.AsTracking()
            .SingleAsync(x => x.SupId == workItem.Hotel.SupplierId && x.SHotelCode == workItem.Hotel.HotelCode, cancellationToken);
        row.LastMatchVersion = matchingVersion;
        row.LastMatchedDataHash = ParseHash(workItem.NormalizedDataHash);
        row.LastMatchTime = DateTime.Now;
        row.NextMatchTime = nextMatchTime?.LocalDateTime;
        row.MatchError = error;
        row.MatchStatus = decision.Kind switch
        {
            HotelMatchDecisionKind.AutoMatched => (byte)HotelMatchStatus.Delivered,
            HotelMatchDecisionKind.NeedsReview => (byte)HotelMatchStatus.NeedsReview,
            HotelMatchDecisionKind.DataProblem => (byte)HotelMatchStatus.DataProblem,
            _ when error is not null => (byte)HotelMatchStatus.Failed,
            _ => (byte)HotelMatchStatus.NoMatch
        };
        row.UpdateTime = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<HotelMatchWorkItem?> GetSupplierHotelAsync(int supId, string supplierHotelCode, CancellationToken cancellationToken)
    {
        var row = await _dbContext.SupHotelInfos.AsNoTracking()
            .Include(x => x.Phones)
            .SingleOrDefaultAsync(x => x.SupId == supId && x.SHotelCode == supplierHotelCode, cancellationToken);
        return row is null ? null : MapWorkItem(row);
    }

    public async Task<CleanedHotel?> GetOtaHotelAsync(string mtHotelCode, CancellationToken cancellationToken)
    {
        var row = await _dbContext.OTAHotelInfos.AsNoTracking()
            .Include(x => x.Phones)
            .SingleOrDefaultAsync(
                x => x.MTHotelCode == mtHotelCode && x.Estate == 1,
                cancellationToken);
        return row is null ? null : MapCleaned(row);
    }

    public async Task<bool> TryCreateAsync(HotelMatchDeliveryCommand command, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.HotelMatchDeliveries.AsTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.DeliveryStatus == DeliveryFailed
                && (!existing.NextRetryTime.HasValue || existing.NextRetryTime <= DateTime.Now))
            {
                existing.DeliveryStatus = DeliveryPending;
                existing.UpdateTime = DateTime.Now;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            return false;
        }

        _dbContext.HotelMatchDeliveries.Add(new HotelMatchDelivery
        {
            SupId = command.SupId,
            SHotelCode = command.SHotelCode,
            MTHotelCode = command.MTHotelCode,
            SourceDataVersion = command.SourceDataVersion,
            NormalizedDataHash = ParseHash(command.NormalizedDataHash),
            CleanVersion = command.CleanVersion,
            MatchingVersion = command.MatchingVersion,
            IdempotencyKey = command.IdempotencyKey,
            DeliveryStatus = DeliveryPending,
            RequestPayload = JsonSerializer.Serialize(command, JsonOptions),
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        });
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task MarkSendingAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var row = await _dbContext.HotelMatchDeliveries.AsTracking().SingleAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        row.DeliveryStatus = DeliverySending;
        row.UpdateTime = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(
        string idempotencyKey,
        HotelMatchDeliveryReceipt receipt,
        DateTimeOffset? nextRetryTime,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.HotelMatchDeliveries.AsTracking().SingleAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        row.DeliveryStatus = receipt.Success ? DeliveryDelivered : DeliveryFailed;
        row.ResponsePayload = receipt.Response;
        row.LastError = receipt.Error;
        row.NextRetryTime = nextRetryTime?.LocalDateTime;
        row.RetryCount += receipt.Success ? 0 : 1;
        row.UpdateTime = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    RawHotelData MapRaw(OTAHotelInfoRaw row)
    {
        var starLevel = NormalizeStarLevel(HotelDataType.Platform, null, null, row.StarLevel);
        return new RawHotelData(HotelDataType.Platform, row.MTHotelCode)
        {
            Id = row.HotelID,
            PlatformHotelId = row.HotelID,
            MtHotelCode = row.MTHotelCode,
            CountryCode = row.CountryCode,
            CountryName = row.CountryName,
            CityCode = row.CityCode,
            CityName = row.CityName,
            EnAddress = row.EnAddress,
            Estate = row.Estate,
            Flag = row.Flag,
            AreaId = row.AreaID,
            CountryId = row.CountryID,
            ProvinceId = row.ProvinceID,
            RootCityId = row.RootCityID,
            CityId = row.CityID,
            CountryNameEn = row.CountryNameEn,
            CityNameEn = row.CityNameEn,
            WebSite = row.WebSite,
            HotelName = row.HotelName,
            HotelNameEn = row.HotelEnName,
            RawStarLevel = row.StarLevel,
            StarLevel = starLevel,
            Telephone = row.Tel,
            Fax = row.Fax,
            GpsType = row.GpsType,
            Longitude = decimal.TryParse(row.Longitude, NumberStyles.Number, CultureInfo.InvariantCulture, out var lon) ? lon : null,
            Latitude = decimal.TryParse(row.Latitude, NumberStyles.Number, CultureInfo.InvariantCulture, out var lat) ? lat : null,
            Address = row.Address,
            PostCode = row.PostCode,
            Email = row.Email,
            UpdatedAt = row.LastModifyTime ?? row.DataChangeTime,
            SourceDataVersion = row.DataVersion,
            SourceDataHash = FormatHash(row.DataHash)
        };
    }

    RawHotelData MapRaw(SupHotelInfoRaw row)
    {
        var starLevel = NormalizeStarLevel(HotelDataType.Supplier, row.SupId, row.SupHotelId, row.StarLevel);
        return new RawHotelData(HotelDataType.Supplier, row.SHotelCode)
        {
            Id = row.SupHotelId,
            SupplierId = row.SupId,
            SourceSystemId = row.SupHotelId,
            MtHotelCode = string.IsNullOrWhiteSpace(row.MTHotelCode) ? null : row.MTHotelCode,
            State = row.State,
            SaleTopLevel = row.SSaleTopLevel,
            IsHandMatch = row.IsHandMatch,
            ReMatchState = row.ReMatchState,
            CountryCode = row.CountryCode,
            CountryName = row.CountryName,
            CityCode = row.CityCode,
            CityName = row.CityName,
            HotelName = row.HotelName,
            HotelNameEn = row.HotelNameEn,
            RawStarLevel = row.StarLevel,
            StarLevel = starLevel,
            Telephone = row.Tel,
            Fax = row.Fax,
            GpsType = int.TryParse(row.GpsType, out var gpsType) ? gpsType : null,
            Longitude = row.Longitude,
            Latitude = row.Latitude,
            Address = row.Address,
            PostCode = row.PostCode,
            Email = row.Email,
            UpdatedAt = row.SourceLastUpdateDate ?? row.SourceUpdateDate ?? row.DataChangeTime,
            SourceDataVersion = row.DataVersion,
            SourceDataHash = FormatHash(row.DataHash)
        };
    }

    decimal? NormalizeStarLevel(
        HotelDataType dataType,
        int? supplierId,
        int? sourceId,
        string? rawValue)
    {
        var normalized = _starLevelNormalizer.Normalize(supplierId, rawValue);
        if (!normalized.Success)
        {
            _starLevelIssueRecorder.Report(dataType, supplierId, sourceId, rawValue, normalized.Reason);
        }

        return normalized.Value;
    }

    static void Map(OTAHotelInfo entity, HotelCleaningResult result, DateTime now)
    {
        var raw = result.Raw;
        var clean = result.Cleaned;
        entity.HotelID = raw.Id.HasValue ? checked((int)Math.Min(int.MaxValue, raw.Id.Value)) : 0;
        entity.HotelID = raw.PlatformHotelId ?? entity.HotelID;
        entity.MTHotelCode = raw.HotelCode;
        entity.HotelName = raw.HotelName ?? raw.HotelNameEn ?? raw.HotelCode;
        entity.HotelEnName = raw.HotelNameEn;
        entity.CountryCode = raw.CountryCode;
        entity.CountryName = raw.CountryName;
        entity.CityCode = raw.CityCode;
        entity.CityName = raw.CityName;
        entity.EnAddress = raw.EnAddress;
        entity.Estate = raw.Estate ?? 0;
        entity.Flag = (byte)Math.Clamp(raw.Flag ?? 0, byte.MinValue, byte.MaxValue);
        entity.AreaID = raw.AreaId ?? 0;
        entity.CountryID = raw.CountryId ?? 0;
        entity.ProvinceID = raw.ProvinceId ?? 0;
        entity.RootCityID = raw.RootCityId ?? 0;
        entity.CityID = raw.CityId ?? 0;
        entity.CountryNameEn = raw.CountryNameEn;
        entity.CityNameEn = raw.CityNameEn;
        entity.WebSite = raw.WebSite;
        entity.Address = raw.Address;
        entity.Tel = raw.Telephone;
        entity.Fax = raw.Fax;
        entity.PostCode = raw.PostCode;
        entity.Email = raw.Email;
        entity.StarLevel = raw.StarLevel;
        entity.CoordType = (byte)(clean.GpsType ?? 0);
        entity.Longitude = clean.Longitude;
        entity.Latitude = clean.Latitude;
        entity.SourceDataVersion = raw.SourceDataVersion;
        entity.SourceDataHash = ParseHash(result.SourceDataHash);
        entity.NormalizedDataHash = ParseHash(result.NormalizedDataHash);
        MapRegions(entity, clean);
        entity.RegionFenceVersion = result.RegionBoundaryVersion;
        entity.RegionResolveStatus = (byte)clean.RegionResolveStatus;
        entity.SourceLastModifyTime = raw.UpdatedAt?.LocalDateTime;
        entity.CleanVersion = result.CleanVersion;
        entity.CleanStatus = (byte)result.CleanStatus;
        entity.CleanError = result.Error;
        entity.DataQualityStatus = (byte)result.Quality.Status;
        entity.DataQualityFlags = (long)result.Quality.Flags;
        entity.DataQualityDetailsJson = JsonSerializer.Serialize(result.Quality.Details, JsonOptions);
        entity.CleanTime = now;
        entity.UpdateTime = now;
    }

    static void Map(SupHotelInfoClean entity, HotelCleaningResult result, DateTime now)
    {
        var raw = result.Raw;
        var clean = result.Cleaned;
        entity.SupId = raw.SupplierId ?? 0;
        entity.SHotelCode = raw.HotelCode;
        entity.MTHotelCode = raw.MtHotelCode ?? string.Empty;
        entity.State = raw.State ?? 0;
        entity.SSaleTopLevel = raw.SaleTopLevel;
        entity.IsHandMatch = raw.IsHandMatch;
        entity.ReMatchState = raw.ReMatchState;
        entity.HotelName = raw.HotelName;
        entity.HotelNameEn = raw.HotelNameEn;
        entity.CountryCode = raw.CountryCode;
        entity.CountryName = raw.CountryName;
        entity.CityCode = raw.CityCode;
        entity.CityName = raw.CityName;
        entity.Tel = raw.Telephone;
        entity.Fax = raw.Fax;
        entity.Address = raw.Address;
        entity.PostCode = raw.PostCode;
        entity.Email = raw.Email;
        entity.StarLevel = raw.StarLevel;
        entity.CoordType = (byte)(clean.GpsType ?? 0);
        entity.Longitude = clean.Longitude;
        entity.Latitude = clean.Latitude;
        MapRegions(entity, clean);
        entity.RegionFenceVersion = result.RegionBoundaryVersion;
        entity.RegionResolveStatus = (byte)clean.RegionResolveStatus;
        entity.SourceDataVersion = raw.SourceDataVersion;
        entity.SourceDataHash = ParseHash(result.SourceDataHash);
        entity.NormalizedDataHash = ParseHash(result.NormalizedDataHash);
        entity.CleanVersion = result.CleanVersion;
        entity.CleanStatus = (byte)result.CleanStatus;
        entity.CleanError = result.Error;
        entity.CleanTime = now;
        entity.DataQualityStatus = (byte)result.Quality.Status;
        entity.DataQualityFlags = (long)result.Quality.Flags;
        entity.DataQualityDetailsJson = JsonSerializer.Serialize(result.Quality.Details, JsonOptions);
        entity.UpdateTime = now;
    }

    static void MapRegions(OTAHotelInfo entity, CleanedHotel clean)
    {
        entity.RegionLevel1Code = CountryCodeNormalizer.ToAlpha2(clean.RegionLevel1Code) ?? clean.RegionLevel1Code;
        entity.RegionLevel1Name = clean.RegionLevel1Name;
        entity.RegionLevel2Code = clean.RegionLevel2Code;
        entity.RegionLevel2Name = clean.RegionLevel2Name;
        entity.RegionLevel3Code = clean.RegionLevel3Code;
        entity.RegionLevel3Name = clean.RegionLevel3Name;
        entity.RegionResolveStatus = (byte)clean.RegionResolveStatus;
    }

    static void MapRegions(SupHotelInfoClean entity, CleanedHotel clean)
    {
        entity.RegionLevel1Code = CountryCodeNormalizer.ToAlpha2(clean.RegionLevel1Code) ?? clean.RegionLevel1Code;
        entity.RegionLevel1Name = clean.RegionLevel1Name;
        entity.RegionLevel2Code = clean.RegionLevel2Code;
        entity.RegionLevel2Name = clean.RegionLevel2Name;
        entity.RegionLevel3Code = clean.RegionLevel3Code;
        entity.RegionLevel3Name = clean.RegionLevel3Name;
        entity.RegionResolveStatus = (byte)clean.RegionResolveStatus;
    }

    static HotelMatchWorkItem MapWorkItem(SupHotelInfoClean row)
        => new(MapCleaned(row), row.SourceDataVersion, FormatHash(row.NormalizedDataHash) ?? string.Empty, row.CleanVersion);

    static HotelMatchFact MapFact(OTAHotelInfo row)
        => new(
            HotelMatchFactSource.Ota,
            row.HotelID,
            null,
            row.MTHotelCode,
            row.MTHotelCode,
            row.HotelName,
            row.HotelEnName,
            row.Longitude,
            row.Latitude,
            row.StarLevel,
            row.Address,
            row.Phones
                .Where(phone => phone.IsValid && phone.NormalizedPhone != null)
                .OrderBy(phone => phone.Ordinal)
                .Select(phone => phone.NormalizedPhone!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    static HotelMatchFact MapFact(SupHotelInfoClean row)
        => new(
            HotelMatchFactSource.Supplier,
            row.SupHotelId,
            row.SupId,
            row.SHotelCode,
            string.IsNullOrWhiteSpace(row.MTHotelCode) ? null : row.MTHotelCode,
            row.HotelName,
            row.HotelNameEn,
            row.Longitude,
            row.Latitude,
            row.StarLevel,
            row.Address,
            row.Phones
                .Where(phone => phone.IsValid && phone.E164 != null)
                .OrderBy(phone => phone.Ordinal)
                .Select(phone => phone.E164!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    static CleanedHotel MapCleaned(OTAHotelInfo row)
        => new(
            HotelDataType.Platform,
            row.MTHotelCode,
            row.HotelName,
            mtHotelCode: row.MTHotelCode,
            countryCode: row.CountryCode,
            countryName: row.CountryName,
            cityCode: row.CityCode,
            cityName: row.CityName,
            hotelNameEn: row.HotelEnName,
            address: row.Address,
            postCode: row.PostCode,
            telephone: row.Tel,
            fax: row.Fax,
            email: row.Email,
            starLevel: row.StarLevel,
            gpsType: row.CoordType,
            longitude: row.Longitude,
            latitude: row.Latitude)
        {
            RegionLevel1Code = row.RegionLevel1Code,
            RegionLevel1Name = row.RegionLevel1Name,
            RegionLevel2Code = row.RegionLevel2Code,
            RegionLevel2Name = row.RegionLevel2Name,
            RegionLevel3Code = row.RegionLevel3Code,
            RegionLevel3Name = row.RegionLevel3Name,
            RegionResolveStatus = (RegionResolveStatus)row.RegionResolveStatus,
            NormalizedPhoneDetails = row.Phones.OrderBy(phone => phone.Ordinal).Select(MapPhone).ToArray(),
            NormalizedPhones = row.Phones.Where(phone => phone.IsValid && phone.NormalizedPhone != null).Select(phone => phone.NormalizedPhone!).Distinct(StringComparer.Ordinal).ToArray(),
            DataQualityStatus = (HotelDataQualityStatus)row.DataQualityStatus,
            DataQualityFlags = (HotelDataQualityFlags)row.DataQualityFlags
        };

    static CleanedHotel MapCleaned(SupHotelInfoClean row)
        => new(
            HotelDataType.Supplier,
            row.SHotelCode,
            row.HotelName ?? string.Empty,
            row.SupId,
            string.IsNullOrWhiteSpace(row.MTHotelCode) ? null : row.MTHotelCode,
            countryCode: row.CountryCode,
            countryName: row.CountryName,
            cityCode: row.CityCode,
            cityName: row.CityName,
            hotelNameEn: row.HotelNameEn,
            address: row.Address,
            postCode: row.PostCode,
            telephone: row.Tel,
            fax: row.Fax,
            email: row.Email,
            starLevel: row.StarLevel,
            gpsType: row.CoordType,
            longitude: row.Longitude,
            latitude: row.Latitude)
        {
            RegionLevel1Code = row.RegionLevel1Code,
            RegionLevel1Name = row.RegionLevel1Name,
            RegionLevel2Code = row.RegionLevel2Code,
            RegionLevel2Name = row.RegionLevel2Name,
            RegionLevel3Code = row.RegionLevel3Code,
            RegionLevel3Name = row.RegionLevel3Name,
            RegionResolveStatus = (RegionResolveStatus)row.RegionResolveStatus,
            NormalizedPhoneDetails = row.Phones.OrderBy(phone => phone.Ordinal).Select(MapPhone).ToArray(),
            NormalizedPhones = row.Phones.Where(phone => phone.IsValid && phone.E164 != null).Select(phone => phone.E164!).Distinct(StringComparer.Ordinal).ToArray(),
            DataQualityStatus = (HotelDataQualityStatus)row.DataQualityStatus,
            DataQualityFlags = (HotelDataQualityFlags)row.DataQualityFlags
        };

    static NormalizedPhone MapPhone(OTAHotelPhone phone)
        => new(phone.SourcePhone, phone.NormalizedPhone, phone.NationalNumber, phone.CountryCallingCode, phone.AreaCode,
            phone.Extension, phone.IsValid, phone.ParseError, phone.CountryIsoCode, phone.NumberType);

    static NormalizedPhone MapPhone(SupHotelPhone phone)
        => new(phone.Raw, phone.E164, phone.NationalNumber, phone.CountryCallingCode, phone.AreaCode,
            phone.Extension, phone.IsValid, phone.Error, phone.RegionCode, phone.NumberType);

    static byte[] ParseHash(string value) => Convert.FromHexString(value);

    static string? FormatHash(byte[]? value) => value is null ? null : Convert.ToHexString(value);

    static string? FormatSqlDateTime(DateTime? value, int hotelId)
    {
        if (value is null)
        {
            return null;
        }

        if (value < SqlDateTimeMinimum || value > SqlDateTimeMaximum)
        {
            throw new InvalidOperationException(
                $"OTA hotel {hotelId} has SourceLastModifyTime outside the SQL datetime range: {value:O}.");
        }

        return value.Value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    void SkipResultsForSession(IEnumerable<HotelCleaningResult> results)
    {
        foreach (var result in results)
        {
            var dataId = result.Raw.Id ?? result.Raw.PlatformHotelId;
            AddSessionSkippedId(result.Raw.Type, dataId);
        }
    }

    void SkipFailuresForSession(IEnumerable<HotelCleaningFailureRecord> failures)
    {
        foreach (var failure in failures)
        {
            AddSessionSkippedId(failure.SourceType, failure.DataId);
        }
    }

    void AddSessionSkippedId(HotelDataType type, long? dataId)
    {
        if (dataId is null or <= 0 or > int.MaxValue)
        {
            return;
        }

        var target = type == HotelDataType.Platform
            ? _sessionSkippedPlatformIds
            : _sessionSkippedSupplierIds;
        target.Add((int)dataId.Value);
    }

    static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    static HotelCleaningFailureRecord CreateFailureRecord(HotelCleaningResult result, Exception exception)
    {
        var raw = result.Raw;
        return new HotelCleaningFailureRecord(
            raw.Type,
            raw.Id ?? raw.PlatformHotelId ?? 0,
            raw.SupplierId,
            raw.SourceSystemId,
            raw.HotelCode,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.ToString(),
            result.CleanVersion,
            result.RegionBoundaryVersion);
    }

    static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

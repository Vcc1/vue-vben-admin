using System.Globalization;
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
    IHotelMatchingRepository,
    IHotelMatchDeliveryRepository
{
    const byte DeliveryPending = 0;
    const byte DeliverySending = 1;
    const byte DeliveryDelivered = 2;
    const byte DeliveryFailed = 3;

    static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    readonly RoomMappingDbContext _dbContext;
    readonly IHotelStarLevelNormalizer _starLevelNormalizer;
    readonly IHotelStarLevelIssueRecorder _starLevelIssueRecorder;
    readonly ILogger<RoomMappingHotelRepository> _logger;
    readonly HashSet<int> _sessionSkippedPlatformIds = [];
    readonly HashSet<int> _sessionSkippedSupplierIds = [];

    public RoomMappingHotelRepository(
        RoomMappingDbContext dbContext,
        IHotelStarLevelNormalizer starLevelNormalizer,
        IHotelStarLevelIssueRecorder starLevelIssueRecorder,
        ILogger<RoomMappingHotelRepository> logger)
    {
        _dbContext = dbContext;
        _starLevelNormalizer = starLevelNormalizer;
        _starLevelIssueRecorder = starLevelIssueRecorder;
        _logger = logger;
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
    }

    public async Task<HotelCleaningProgress> GetCleaningProgressAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        CancellationToken cancellationToken)
    {
        var changedSinceLocal = changedSince.LocalDateTime;
        if (type == HotelDataType.Platform)
        {
            var total = await _dbContext.OTAHotelInfoRaws.LongCountAsync(cancellationToken);
            var cleaned = await (
                from raw in _dbContext.OTAHotelInfoRaws
                join clean in _dbContext.OTAHotelInfos on raw.HotelID equals clean.HotelID
                where clean.CleanVersion == cleanVersion
                    && clean.RegionFenceVersion == regionBoundaryVersion
                    && clean.SourceDataVersion == raw.DataVersion
                    && !(raw.DataChangeTime >= changedSinceLocal && clean.CleanTime < raw.DataChangeTime)
                select raw.HotelID).LongCountAsync(cancellationToken);
            var failed = await (
                from raw in _dbContext.OTAHotelInfoRaws
                join failure in _dbContext.HotelCleaningFailures
                    on new { SourceType = (byte)HotelDataType.Platform, DataId = (long)raw.HotelID }
                    equals new { failure.SourceType, failure.DataId }
                where failure.CleanVersion == cleanVersion
                    && failure.RegionBoundaryVersion == regionBoundaryVersion
                select failure.Id).LongCountAsync(cancellationToken);
            return new HotelCleaningProgress(total, cleaned, failed);
        }

        var supplierTotal = await _dbContext.SupHotelInfoRaws.LongCountAsync(cancellationToken);
        var supplierCleaned = await (
            from raw in _dbContext.SupHotelInfoRaws
            join clean in _dbContext.SupHotelInfos on raw.SupHotelId equals clean.SupHotelId
            where clean.CleanVersion == cleanVersion
                && clean.RegionFenceVersion == regionBoundaryVersion
                && clean.SourceDataVersion == raw.DataVersion
                && !(raw.DataChangeTime >= changedSinceLocal && clean.CleanTime < raw.DataChangeTime)
            select raw.SupHotelId).LongCountAsync(cancellationToken);
        var supplierFailed = await (
            from raw in _dbContext.SupHotelInfoRaws
            join failure in _dbContext.HotelCleaningFailures
                on new { SourceType = (byte)HotelDataType.Supplier, DataId = (long)raw.SupHotelId }
                equals new { failure.SourceType, failure.DataId }
            where failure.CleanVersion == cleanVersion
                && failure.RegionBoundaryVersion == regionBoundaryVersion
            select failure.Id).LongCountAsync(cancellationToken);
        return new HotelCleaningProgress(supplierTotal, supplierCleaned, supplierFailed);
    }

    public async Task<IReadOnlyList<RawHotelData>> GetHotelsToCleanAsync(
        HotelDataType type,
        DateTimeOffset changedSince,
        int cleanVersion,
        int regionBoundaryVersion,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (type == HotelDataType.Platform)
        {
            var sessionSkippedIds = _sessionSkippedPlatformIds.ToArray();
            var rows = await (
                from raw in _dbContext.OTAHotelInfoRaws.AsNoTracking()
                join clean in _dbContext.OTAHotelInfos.AsNoTracking() on raw.HotelID equals clean.HotelID into cleanRows
                from clean in cleanRows.DefaultIfEmpty()
                where !sessionSkippedIds.Contains(raw.HotelID)
                    && !_dbContext.HotelCleaningFailures.Any(f => f.SourceType == (byte)HotelDataType.Platform && f.DataId == raw.HotelID)
                    && (clean == null
                        || clean.CleanVersion != cleanVersion
                        || clean.RegionFenceVersion != regionBoundaryVersion
                        || clean.SourceDataVersion != raw.DataVersion
                        || raw.DataChangeTime >= changedSince.LocalDateTime && clean.CleanTime < raw.DataChangeTime)
                orderby raw.DataChangeTime, raw.HotelID
                select raw)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            return rows.Select(MapRaw).ToArray();
        }

        var sessionSkippedSupplierIds = _sessionSkippedSupplierIds.ToArray();
        var supplierRows = await (
            from raw in _dbContext.SupHotelInfoRaws.AsNoTracking()
            join clean in _dbContext.SupHotelInfos.AsNoTracking() on raw.SupHotelId equals clean.SupHotelId into cleanRows
            from clean in cleanRows.DefaultIfEmpty()
            where !sessionSkippedSupplierIds.Contains(raw.SupHotelId)
                && !_dbContext.HotelCleaningFailures.Any(f => f.SourceType == (byte)HotelDataType.Supplier && f.DataId == raw.SupHotelId)
                && (clean == null
                    || clean.CleanVersion != cleanVersion
                    || clean.RegionFenceVersion != regionBoundaryVersion
                    || clean.SourceDataVersion != raw.DataVersion
                    || raw.DataChangeTime >= changedSince.LocalDateTime && clean.CleanTime < raw.DataChangeTime)
            orderby raw.DataChangeTime, raw.SupHotelId
            select raw)
            .Take(batchSize)
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
        catch (DbUpdateException batchException) when (!IsTransientSqlFailure(batchException))
        {
            _dbContext.ChangeTracker.Clear();
            if (results.Count == 1)
            {
                await RecordCleaningFailuresAsync([CreateFailureRecord(results.Single(), batchException)], cancellationToken);
                return 0;
            }

            var savedCount = 0;
            foreach (var result in results)
            {
                try
                {
                    await SaveCleanedHotelsWithRetryAsync([result], cancellationToken);
                    savedCount++;
                }
                catch (DbUpdateException ex) when (!IsTransientSqlFailure(ex))
                {
                    _dbContext.ChangeTracker.Clear();
                    await RecordCleaningFailuresAsync([CreateFailureRecord(result, ex)], cancellationToken);
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
                    await RecordCleaningFailuresAsync([CreateFailureRecord(result, ex)], cancellationToken);
                }
            }

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
                var hotelIds = group.Select(x => x.Raw.PlatformHotelId ?? checked((int)(x.Raw.Id ?? 0))).Where(x => x > 0).ToArray();
                var existing = await _dbContext.OTAHotelInfos.AsTracking()
                    .Include(x => x.Phones)
                    .Where(x => hotelIds.Contains(x.HotelID))
                    .ToDictionaryAsync(x => x.HotelID, cancellationToken);
                foreach (var result in group)
                {
                    var hotelId = result.Raw.PlatformHotelId ?? checked((int)(result.Raw.Id ?? 0));
                    if (hotelId <= 0)
                    {
                        throw new InvalidOperationException("OTA raw row is missing HotelID.");
                    }

                    if (!existing.TryGetValue(hotelId, out var entity))
                    {
                        entity = new OTAHotelInfo { HotelID = hotelId, CreateTime = now };
                        _dbContext.OTAHotelInfos.Add(entity);
                    }

                    Map(entity, result, now);
                    ReplacePhones(entity.HotelID, entity.Phones, result.Cleaned.NormalizedPhoneDetails, now);
                }
            }
            else
            {
                var supHotelIds = group.Select(x => checked((int)(x.Raw.Id ?? 0))).Where(x => x > 0).ToArray();
                var existing = await _dbContext.SupHotelInfos.AsTracking()
                    .Include(x => x.Phones)
                    .Where(x => supHotelIds.Contains(x.SupHotelId))
                    .ToDictionaryAsync(x => x.SupHotelId, cancellationToken);
                foreach (var result in group)
                {
                    var supHotelId = checked((int)(result.Raw.Id ?? 0));
                    if (supHotelId <= 0)
                    {
                        throw new InvalidOperationException("Supplier raw row is missing SupHotelId.");
                    }

                    if (!existing.TryGetValue(supHotelId, out var entity))
                    {
                        entity = new SupHotelInfoClean { SupHotelId = supHotelId, CreateTime = now };
                        _dbContext.SupHotelInfos.Add(entity);
                    }

                    var normalizedChanged = !BytesEqual(entity.NormalizedDataHash, ParseHash(result.NormalizedDataHash));
                    Map(entity, result, now);
                    ReplacePhones(entity.SupHotelId, entity.Phones, result.Cleaned.NormalizedPhoneDetails, now);
                    if (!result.IsMatchable)
                    {
                        entity.MatchStatus = (byte)HotelMatchStatus.DataProblem;
                    }
                    else if (normalizedChanged && string.IsNullOrWhiteSpace(entity.MTHotelCode))
                    {
                        entity.MatchStatus = (byte)HotelMatchStatus.Pending;
                        entity.MatchError = null;
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
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
        foreach (var failure in failures
                     .GroupBy(x => new { x.SourceType, x.DataId })
                     .Select(x => x.Last()))
        {
            var sourceType = (byte)failure.SourceType;
            var entity = await _dbContext.HotelCleaningFailures.AsTracking()
                .SingleOrDefaultAsync(
                    x => x.SourceType == sourceType && x.DataId == failure.DataId,
                    cancellationToken);
            if (entity is null)
            {
                entity = new HotelCleaningFailure
                {
                    SourceType = sourceType,
                    DataId = failure.DataId,
                    FirstFailureTime = now,
                    FailureCount = 0
                };
                _dbContext.HotelCleaningFailures.Add(entity);
            }

            entity.SupplierId = failure.SupplierId;
            entity.SourceId = failure.SourceId;
            entity.HotelCode = Truncate(failure.HotelCode, 200);
            entity.ExceptionType = Truncate(failure.ExceptionType, 500);
            entity.ExceptionMessage = Truncate(failure.ExceptionMessage, 4000);
            entity.StackTrace = failure.StackTrace;
            entity.CleanVersion = failure.CleanVersion;
            entity.RegionBoundaryVersion = failure.RegionBoundaryVersion;
            entity.FailureCount++;
            entity.LastFailureTime = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<HotelMatchWorkItem>> ClaimUnmatchedSupplierHotelsAsync(
        int matchingVersion,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var activeDeliveryStates = new[] { DeliveryPending, DeliverySending, DeliveryDelivered };
        var now = DateTime.Now;
        var rows = await _dbContext.SupHotelInfos.AsTracking()
            .Include(x => x.Phones)
            .Where(x => x.CleanStatus == (byte)HotelCleanStatus.Ready
                && x.DataQualityStatus == (byte)HotelDataQualityStatus.Valid
                && x.Longitude.HasValue
                && x.Latitude.HasValue
                && string.IsNullOrEmpty(x.MTHotelCode)
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
        var longitude = supplier.Hotel.Longitude ?? throw new InvalidOperationException("Supplier longitude is missing.");
        var latitude = supplier.Hotel.Latitude ?? throw new InvalidOperationException("Supplier latitude is missing.");
        var rows = await _dbContext.OTAHotelInfos
            .FromSqlInterpolated($$"""
                SELECT TOP ({{maxCandidates}}) o.*
                FROM dbo.OTAHotelInfo AS o
                WHERE o.CleanStatus = {{(byte)HotelCleanStatus.Ready}}
                  AND o.DataQualityStatus = {{(byte)HotelDataQualityStatus.Valid}}
                  AND o.GeoPoint IS NOT NULL
                  AND o.GeoPoint.STDistance(geography::Point({{latitude}}, {{longitude}}, 4326)) <= {{radiusMeters}}
                ORDER BY o.GeoPoint.STDistance(geography::Point({{latitude}}, {{longitude}}, 4326))
                """)
            .Include(x => x.Phones)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return rows.Select(MapCleaned).ToArray();
    }

    public async Task<IReadOnlyList<CleanedHotel>> RecallSupplierCandidatesAsync(
        CleanedHotel ota,
        int radiusMeters,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var longitude = ota.Longitude ?? throw new InvalidOperationException("OTA longitude is missing.");
        var latitude = ota.Latitude ?? throw new InvalidOperationException("OTA latitude is missing.");
        var rows = await _dbContext.SupHotelInfos
            .FromSqlInterpolated($$"""
                SELECT TOP ({{maxCandidates}}) s.*
                FROM dbo.SupHotelInfo AS s
                WHERE s.CleanStatus = {{(byte)HotelCleanStatus.Ready}}
                  AND s.DataQualityStatus = {{(byte)HotelDataQualityStatus.Valid}}
                  AND s.GeoPoint IS NOT NULL
                  AND s.GeoPoint.STDistance(geography::Point({{latitude}}, {{longitude}}, 4326)) <= {{radiusMeters}}
                ORDER BY s.GeoPoint.STDistance(geography::Point({{latitude}}, {{longitude}}, 4326))
                """)
            .Include(x => x.Phones)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return rows.Select(MapCleaned).ToArray();
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
            .SingleOrDefaultAsync(x => x.MTHotelCode == mtHotelCode, cancellationToken);
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
        entity.RegionLevel1Code = clean.RegionLevel1Code;
        entity.RegionLevel1Name = clean.RegionLevel1Name;
        entity.RegionLevel2Code = clean.RegionLevel2Code;
        entity.RegionLevel2Name = clean.RegionLevel2Name;
        entity.RegionLevel3Code = clean.RegionLevel3Code;
        entity.RegionLevel3Name = clean.RegionLevel3Name;
        entity.RegionResolveStatus = (byte)clean.RegionResolveStatus;
    }

    static void MapRegions(SupHotelInfoClean entity, CleanedHotel clean)
    {
        entity.RegionLevel1Code = clean.RegionLevel1Code;
        entity.RegionLevel1Name = clean.RegionLevel1Name;
        entity.RegionLevel2Code = clean.RegionLevel2Code;
        entity.RegionLevel2Name = clean.RegionLevel2Name;
        entity.RegionLevel3Code = clean.RegionLevel3Code;
        entity.RegionLevel3Name = clean.RegionLevel3Name;
        entity.RegionResolveStatus = (byte)clean.RegionResolveStatus;
    }

    static HotelMatchWorkItem MapWorkItem(SupHotelInfoClean row)
        => new(MapCleaned(row), row.SourceDataVersion, FormatHash(row.NormalizedDataHash) ?? string.Empty, row.CleanVersion);

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

    static void ReplacePhones(int hotelId, ICollection<OTAHotelPhone> target, IReadOnlyList<NormalizedPhone> source, DateTime now)
    {
        var existing = target.ToDictionary(x => x.Ordinal);
        for (var index = 0; index < source.Count; index++)
        {
            var phone = source[index];
            var ordinal = checked((short)index);
            if (!existing.Remove(ordinal, out var entity))
            {
                entity = new OTAHotelPhone
                {
                    HotelID = hotelId,
                    Ordinal = ordinal,
                    CreateTime = now
                };
                target.Add(entity);
            }

            entity.SourcePhone = phone.Raw;
            entity.NormalizedPhone = phone.E164;
            entity.CountryIsoCode = phone.RegionCode;
            entity.NationalNumber = phone.NationalNumber;
            entity.CountryCallingCode = phone.CountryCallingCode;
            entity.NumberType = phone.NumberType;
            entity.AreaCode = phone.AreaCode;
            entity.Extension = phone.Extension;
            entity.IsValid = phone.IsValid;
            entity.ParseError = phone.Error;
            entity.UpdateTime = now;
        }

        foreach (var removed in existing.Values)
        {
            target.Remove(removed);
        }
    }

    static void ReplacePhones(int supHotelId, ICollection<SupHotelPhone> target, IReadOnlyList<NormalizedPhone> source, DateTime now)
    {
        target.Clear();
        for (var index = 0; index < source.Count; index++)
        {
            var phone = source[index];
            target.Add(new SupHotelPhone
            {
                SupHotelId = supHotelId, Ordinal = checked((short)index), Raw = phone.Raw, E164 = phone.E164,
                NationalNumber = phone.NationalNumber, CountryCallingCode = phone.CountryCallingCode,
                RegionCode = phone.RegionCode, NumberType = phone.NumberType, AreaCode = phone.AreaCode,
                Extension = phone.Extension, IsValid = phone.IsValid, Error = phone.Error,
                CreateTime = now, UpdateTime = now
            });
        }
    }

    static byte[] ParseHash(string value) => Convert.FromHexString(value);

    static string? FormatHash(byte[]? value) => value is null ? null : Convert.ToHexString(value);

    static bool BytesEqual(byte[]? left, byte[] right) => left is not null && left.AsSpan().SequenceEqual(right);

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

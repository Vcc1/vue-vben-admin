using System.Globalization;
using System.Text.Json;
using HotelMatchingPipeline.App.Configuration;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using HotelMatchingPipeline.Domain.HotelNameMatching;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tests.Host.HotelMappingSync;

namespace Tests.Host.Controllers;

[ApiController]
[Route("api/hotel-match-data")]
public sealed class HotelMatchDataController : ControllerBase
{
    readonly RoomMappingDbContext _dbContext;
    readonly IHotelMatchPipelineRepository _pipelineRepository;
    readonly HotelFactMatchEvaluator _evaluator;
    readonly HotelNameMatcher _nameMatcher;
    readonly HotelMatchingOptions _options;
    readonly HotelMappingRedisSyncWorker _redisSyncWorker;

    public HotelMatchDataController(
        RoomMappingDbContext dbContext,
        IHotelMatchPipelineRepository pipelineRepository,
        HotelFactMatchEvaluator evaluator,
        HotelNameMatcher nameMatcher,
        IOptions<HotelMatchingOptions> options,
        HotelMappingRedisSyncWorker redisSyncWorker)
    {
        _dbContext = dbContext;
        _pipelineRepository = pipelineRepository;
        _evaluator = evaluator;
        _nameMatcher = nameMatcher;
        _options = options.Value;
        _redisSyncWorker = redisSyncWorker;
    }

    [HttpPost("redis-sync")]
    public IActionResult StartRedisSync()
    {
        return _redisSyncWorker.TryStart(out var status)
            ? Accepted(status)
            : Conflict(status);
    }

    [HttpGet("redis-sync/status")]
    public ActionResult<HotelMappingRedisSyncStatus> GetRedisSyncStatus()
    {
        return Ok(_redisSyncWorker.GetStatus());
    }

    [HttpGet("results")]
    public async Task<object> GetResultsAsync(
        int? supId,
        string? supplierHotelCode,
        string? mtHotelCode,
        string? regionCode,
        string? status,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = _dbContext.HotelMatchResults.AsNoTracking();
        if (supId.HasValue)
        {
            query = query.Where(row => row.SupplierHotel.SupId == supId.Value);
        }
        if (!string.IsNullOrWhiteSpace(supplierHotelCode))
        {
            var code = supplierHotelCode.Trim();
            query = query.Where(row => row.SupplierHotel.SHotelCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(mtHotelCode))
        {
            var code = mtHotelCode.Trim();
            query = query.Where(row => row.MTHotelCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(regionCode))
        {
            var code = regionCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.SupplierHotel.RegionLevel1Code == code);
        }
        query = status?.Trim().ToLowerInvariant() switch
        {
            "matched" => query.Where(row => row.MTHotelCode != string.Empty),
            "unmatched" => query.Where(row => row.MTHotelCode == string.Empty),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.UpdateTime)
            .ThenByDescending(row => row.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.Id,
                row.SupHotelId,
                row.SupplierHotel.SupId,
                supplierHotelCode = row.SupplierHotel.SHotelCode,
                supplierHotelName = row.SupplierHotel.HotelName,
                supplierHotelNameEn = row.SupplierHotel.HotelNameEn,
                supplierCountryCode = row.SupplierHotel.CountryCode,
                supplierCountryName = row.SupplierHotel.CountryName,
                supplierRegionCode = row.SupplierHotel.RegionLevel1Code,
                supplierRegionName = row.SupplierHotel.RegionLevel1Name,
                supplierPhone = row.SupplierHotel.Tel,
                supplierStarLevel = row.SupplierHotel.StarLevel,
                row.MTHotelCode,
                isMatched = row.MTHotelCode != string.Empty,
                row.NameMatchKind,
                row.DistanceMeters,
                row.OtaFactCount,
                row.SupplierFactCount,
                row.CreateTime,
                row.UpdateTime
            })
            .ToArrayAsync(cancellationToken);

        return new { page, pageSize, total, items = rows };
    }

    [HttpGet("conflicts")]
    public async Task<object> GetConflictsAsync(
        int? supId,
        string? supplierHotelCode,
        string? mtHotelCode,
        string? regionCode,
        string? status,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = _dbContext.HotelMatchConflicts.AsNoTracking();
        if (supId.HasValue)
        {
            query = query.Where(row => row.SupId == supId.Value);
        }
        if (!string.IsNullOrWhiteSpace(supplierHotelCode))
        {
            var code = supplierHotelCode.Trim();
            query = query.Where(row => row.SupHotelCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(mtHotelCode))
        {
            var code = mtHotelCode.Trim();
            query = query.Where(row => row.MTHotelCodesJson.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(regionCode))
        {
            var code = regionCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.SupplierHotel.RegionLevel1Code == code);
        }
        query = status?.Trim().ToLowerInvariant() switch
        {
            "active" => query.Where(row => row.IsActive),
            "resolved" => query.Where(row => !row.IsActive),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.IsActive)
            .ThenByDescending(row => row.UpdateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.Id,
                row.SupHotelId,
                row.SupId,
                supplierHotelCode = row.SupHotelCode,
                supplierHotelName = row.SupplierHotel.HotelName,
                supplierHotelNameEn = row.SupplierHotel.HotelNameEn,
                supplierCountryCode = row.SupplierHotel.CountryCode,
                supplierCountryName = row.SupplierHotel.CountryName,
                supplierRegionCode = row.SupplierHotel.RegionLevel1Code,
                supplierRegionName = row.SupplierHotel.RegionLevel1Name,
                supplierPhone = row.SupplierHotel.Tel,
                supplierStarLevel = row.SupplierHotel.StarLevel,
                row.CandidateCount,
                row.MTHotelCodesJson,
                row.MatchingVersion,
                row.IsActive,
                row.FirstConflictTime,
                row.LastConflictTime,
                row.ResolvedTime,
                row.UpdateTime
            })
            .ToArrayAsync(cancellationToken);

        return new
        {
            page,
            pageSize,
            total,
            items = rows.Select(row => new
            {
                row.Id,
                row.SupHotelId,
                row.SupId,
                row.supplierHotelCode,
                row.supplierHotelName,
                row.supplierHotelNameEn,
                row.supplierCountryCode,
                row.supplierCountryName,
                row.supplierRegionCode,
                row.supplierRegionName,
                row.supplierPhone,
                row.supplierStarLevel,
                row.CandidateCount,
                mtHotelCodes = ReadStringArray(row.MTHotelCodesJson),
                row.MatchingVersion,
                row.IsActive,
                row.FirstConflictTime,
                row.LastConflictTime,
                row.ResolvedTime,
                row.UpdateTime
            })
        };
    }

    [HttpGet("deliveries")]
    public async Task<object> GetDeliveriesAsync(
        int? supId,
        string? supplierHotelCode,
        string? mtHotelCode,
        string? regionCode,
        int? status,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = _dbContext.HotelMatchDeliveries.AsNoTracking();
        if (supId.HasValue)
        {
            query = query.Where(row => row.SupId == supId.Value);
        }
        if (!string.IsNullOrWhiteSpace(supplierHotelCode))
        {
            var code = supplierHotelCode.Trim();
            query = query.Where(row => row.SHotelCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(mtHotelCode))
        {
            var code = mtHotelCode.Trim();
            query = query.Where(row => row.MTHotelCode.Contains(code));
        }
        if (!string.IsNullOrWhiteSpace(regionCode))
        {
            var code = regionCode.Trim().ToUpperInvariant();
            query = query.Where(row => _dbContext.SupHotelInfos.Any(supplier =>
                supplier.SupId == row.SupId
                && supplier.SHotelCode == row.SHotelCode
                && supplier.RegionLevel1Code == code));
        }
        if (status.HasValue)
        {
            query = query.Where(row => row.DeliveryStatus == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.UpdateTime)
            .ThenByDescending(row => row.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.Id,
                row.SupId,
                supplierHotelCode = row.SHotelCode,
                row.MTHotelCode,
                row.DeliveryStatus,
                row.RetryCount,
                row.NextRetryTime,
                row.MatchingVersion,
                row.LastError,
                row.CreateTime,
                row.UpdateTime
            })
            .ToArrayAsync(cancellationToken);

        var supIds = rows.Select(row => row.SupId).Distinct().ToArray();
        var supplierCodes = rows.Select(row => row.supplierHotelCode).Distinct().ToArray();
        var supplierInfos = await _dbContext.SupHotelInfos
            .AsNoTracking()
            .Where(row => supIds.Contains(row.SupId) && supplierCodes.Contains(row.SHotelCode))
            .Select(row => new
            {
                row.SupId,
                row.SHotelCode,
                row.HotelName,
                row.HotelNameEn,
                row.CountryCode,
                row.CountryName,
                row.RegionLevel1Code,
                row.RegionLevel1Name,
                row.Tel,
                row.StarLevel
            })
            .ToArrayAsync(cancellationToken);
        var infoByKey = supplierInfos.ToDictionary(
            row => SupplierKey(row.SupId, row.SHotelCode));

        return new
        {
            page,
            pageSize,
            total,
            items = rows.Select(row =>
            {
                infoByKey.TryGetValue(
                    SupplierKey(row.SupId, row.supplierHotelCode),
                    out var supplier);
                return new
                {
                    row.Id,
                    row.SupId,
                    row.supplierHotelCode,
                    supplierHotelName = supplier?.HotelName,
                    supplierHotelNameEn = supplier?.HotelNameEn,
                    supplierCountryCode = supplier?.CountryCode,
                    supplierCountryName = supplier?.CountryName,
                    supplierRegionCode = supplier?.RegionLevel1Code,
                    supplierRegionName = supplier?.RegionLevel1Name,
                    supplierPhone = supplier?.Tel,
                    supplierStarLevel = supplier?.StarLevel,
                    row.MTHotelCode,
                    row.DeliveryStatus,
                    row.RetryCount,
                    row.NextRetryTime,
                    row.MatchingVersion,
                    row.LastError,
                    row.CreateTime,
                    row.UpdateTime
                };
            })
        };
    }

    [HttpGet("{kind}/{id:long}/detail")]
    public async Task<IActionResult> GetDetailAsync(
        string kind,
        long id,
        CancellationToken cancellationToken)
    {
        var context = await LoadRecordAsync(kind, id, cancellationToken);
        if (context is null)
        {
            return NotFound(new { message = "记录不存在。" });
        }

        if (context.Supplier is null)
        {
            return Ok(new
            {
                context.Kind,
                context.Record,
                current = (object?)null,
                recallError = "找不到对应的供应商酒店清洗数据。"
            });
        }

        try
        {
            var workItem = await _pipelineRepository.LoadMatchingWorkItemAsync(
                context.Supplier.SupHotelId,
                _options.RecallRadiusMeters,
                _options.MaxCandidates,
                cancellationToken);
            var evaluation = _evaluator.Evaluate(workItem, _options.MatchDistanceMeters);
            var effectiveRecall = HotelRecallPolicy.Resolve(
                context.Supplier.RegionLevel1Code,
                context.Supplier.CountryCode,
                _options.RecallRadiusMeters,
                _options.MaxCandidates);

            return Ok(new
            {
                context.Kind,
                context.Record,
                current = await BuildCurrentAnalysisAsync(
                    workItem,
                    evaluation,
                    effectiveRecall,
                    _options.MatchDistanceMeters,
                    cancellationToken),
                recallError = (string?)null
            });
        }
        catch (Exception exception)
        {
            return Ok(new
            {
                context.Kind,
                context.Record,
                current = (object?)null,
                recallError = exception.Message
            });
        }
    }

    async Task<RecordContext?> LoadRecordAsync(
        string kind,
        long id,
        CancellationToken cancellationToken)
    {
        switch (kind.Trim().ToLowerInvariant())
        {
            case "results":
            {
                var row = await _dbContext.HotelMatchResults
                    .AsNoTracking()
                    .Include(item => item.SupplierHotel)
                    .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (row is null)
                {
                    return null;
                }

                return new RecordContext(
                    "results",
                    row.SupplierHotel,
                    new
                    {
                        row.Id,
                        row.SupHotelId,
                        row.SupplierHotel.SupId,
                        supplierHotelCode = row.SupplierHotel.SHotelCode,
                        supplierHotelName = row.SupplierHotel.HotelName,
                        supplierHotelNameEn = row.SupplierHotel.HotelNameEn,
                        supplierCountryCode = row.SupplierHotel.CountryCode,
                        supplierCountryName = row.SupplierHotel.CountryName,
                        supplierRegionCode = row.SupplierHotel.RegionLevel1Code,
                        supplierRegionName = row.SupplierHotel.RegionLevel1Name,
                        supplierPhone = row.SupplierHotel.Tel,
                        supplierStarLevel = row.SupplierHotel.StarLevel,
                        row.MTHotelCode,
                        isMatched = !string.IsNullOrWhiteSpace(row.MTHotelCode),
                        row.DistanceMeters,
                        row.NameMatchKind,
                        row.OtaFactCount,
                        row.SupplierFactCount,
                        evidence = ReadJson(row.EvidenceJson),
                        row.CreateTime,
                        row.UpdateTime
                    });
            }
            case "conflicts":
            {
                var row = await _dbContext.HotelMatchConflicts
                    .AsNoTracking()
                    .Include(item => item.SupplierHotel)
                    .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (row is null)
                {
                    return null;
                }

                return new RecordContext(
                    "conflicts",
                    row.SupplierHotel,
                    new
                    {
                        row.Id,
                        row.SupHotelId,
                        row.SupId,
                        supplierHotelCode = row.SupHotelCode,
                        supplierHotelName = row.SupplierHotel.HotelName,
                        supplierHotelNameEn = row.SupplierHotel.HotelNameEn,
                        supplierCountryCode = row.SupplierHotel.CountryCode,
                        supplierCountryName = row.SupplierHotel.CountryName,
                        supplierRegionCode = row.SupplierHotel.RegionLevel1Code,
                        supplierRegionName = row.SupplierHotel.RegionLevel1Name,
                        supplierPhone = row.SupplierHotel.Tel,
                        supplierStarLevel = row.SupplierHotel.StarLevel,
                        row.CandidateCount,
                        mtHotelCodes = ReadStringArray(row.MTHotelCodesJson),
                        candidates = ReadJson(row.CandidatesJson),
                        row.MatchingVersion,
                        row.IsActive,
                        row.FirstConflictTime,
                        row.LastConflictTime,
                        row.ResolvedTime,
                        row.CreateTime,
                        row.UpdateTime
                    });
            }
            case "deliveries":
            {
                var row = await _dbContext.HotelMatchDeliveries
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (row is null)
                {
                    return null;
                }

                var supplier = await _dbContext.SupHotelInfos
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.SupId == row.SupId && item.SHotelCode == row.SHotelCode,
                        cancellationToken);
                return new RecordContext(
                    "deliveries",
                    supplier,
                    new
                    {
                        row.Id,
                        row.SupId,
                        supplierHotelCode = row.SHotelCode,
                        supplierHotelName = supplier?.HotelName,
                        supplierHotelNameEn = supplier?.HotelNameEn,
                        supplierCountryCode = supplier?.CountryCode,
                        supplierCountryName = supplier?.CountryName,
                        supplierRegionCode = supplier?.RegionLevel1Code,
                        supplierRegionName = supplier?.RegionLevel1Name,
                        supplierPhone = supplier?.Tel,
                        supplierStarLevel = supplier?.StarLevel,
                        row.MTHotelCode,
                        row.DeliveryStatus,
                        row.RetryCount,
                        row.NextRetryTime,
                        row.SourceDataVersion,
                        normalizedDataHash = Convert.ToHexString(row.NormalizedDataHash),
                        row.CleanVersion,
                        row.MatchingVersion,
                        row.IdempotencyKey,
                        request = ReadJson(row.RequestPayload),
                        response = ReadJsonOrText(row.ResponsePayload),
                        row.LastError,
                        row.CreateTime,
                        row.UpdateTime
                    });
            }
            default:
                return null;
        }
    }

    async Task<object> BuildCurrentAnalysisAsync(
        HotelMatchingWorkItem workItem,
        HotelMatchEvaluation evaluation,
        HotelRecallParameters effectiveRecall,
        int matchDistanceMeters,
        CancellationToken cancellationToken)
    {
        var metadata = await LoadFactMetadataAsync(workItem, cancellationToken);
        var matchedByCode = evaluation.Matches.ToDictionary(
            match => match.MTHotelCode,
            StringComparer.OrdinalIgnoreCase);
        var candidates = workItem.NearbyOtaHotels
            .Where(fact => !string.IsNullOrWhiteSpace(fact.MTHotelCode))
            .GroupBy(fact => fact.MTHotelCode!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var facts = group
                    .Concat(workItem.NearbySupplierHotels.Where(fact =>
                        string.Equals(fact.MTHotelCode, group.Key, StringComparison.OrdinalIgnoreCase)))
                    .Concat(workItem.OtaLinkedSupplierHotels.Where(fact =>
                        string.Equals(fact.MTHotelCode, group.Key, StringComparison.OrdinalIgnoreCase)))
                    .DistinctBy(fact => (fact.Source, fact.HotelId))
                    .ToArray();
                matchedByCode.TryGetValue(group.Key, out var match);
                var nameEvidence = FindNameEvidence(workItem.PendingSupplier, facts);
                var nearestCoordinate = FindNearestCoordinate(workItem.PendingSupplier, facts);
                var primaryOtaFact = group.First();
                metadata.TryGetValue(
                    FactKey(primaryOtaFact.Source, primaryOtaFact.HotelId),
                    out var candidateMetadata);
                var evidenceRows = BuildEvidenceRows(
                    workItem.PendingSupplier,
                    facts,
                    match,
                    nameEvidence,
                    nearestCoordinate,
                    matchDistanceMeters,
                    metadata);

                return new
                {
                    mtHotelCode = group.Key,
                    hotelName = group
                        .Select(fact => fact.HotelName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    hotelNameEn = group
                        .Select(fact => fact.HotelNameEn)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    regionCode = candidateMetadata?.RegionCode,
                    regionName = candidateMetadata?.RegionName,
                    isMatched = match is not null,
                    minimumDistanceMeters = nearestCoordinate?.DistanceMeters,
                    nameMatched = nameEvidence is not null,
                    coordinateMatched = nearestCoordinate is not null
                        && nearestCoordinate.DistanceMeters <= matchDistanceMeters,
                    failedConditions = new[]
                    {
                        nameEvidence is null ? "没有找到相同的酒店名称" : null,
                        nearestCoordinate is null
                            ? "没有可比较的经纬度"
                            : nearestCoordinate.DistanceMeters > matchDistanceMeters
                                ? $"最近距离 {nearestCoordinate.DistanceMeters:0.0} 米，超过 {matchDistanceMeters} 米阈值"
                                : null
                    }.Where(reason => reason is not null),
                    otaFactCount = facts.Count(fact => fact.Source == HotelMatchFactSource.Ota),
                    supplierFactCount = facts.Count(fact => fact.Source == HotelMatchFactSource.Supplier),
                    evidenceRows,
                    facts = facts.Select(fact => ToFactView(fact, metadata))
                };
            })
            .OrderByDescending(candidate => candidate.isMatched)
            .ThenBy(candidate => candidate.minimumDistanceMeters ?? double.MaxValue)
            .ToArray();

        return new
        {
            supplier = ToFactView(workItem.PendingSupplier, metadata),
            settings = new
            {
                recallRadiusMeters = effectiveRecall.RadiusMeters,
                effectiveRecall.MaxCandidates,
                matchDistanceMeters
            },
            matchedMTHotelCodes = evaluation.Matches.Select(match => match.MTHotelCode).ToArray(),
            candidateCount = candidates.Length,
            candidates
        };
    }

    IReadOnlyList<EvidenceRowView> BuildEvidenceRows(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts,
        HotelMatchedCandidate? match,
        NameEvidenceInfo? foundName,
        CoordinateEvidenceInfo? nearestCoordinate,
        int matchDistanceMeters,
        IReadOnlyDictionary<string, FactMetadata> metadata)
    {
        var rows = new List<EvidenceRowView>();
        var nameEvidence = match?.NameEvidence;
        var nameFact = nameEvidence is null
            ? foundName?.Fact
            : FindFact(facts, nameEvidence.Source, nameEvidence.HotelId);
        rows.Add(new EvidenceRowView(
            "酒店名称",
            true,
            nameEvidence?.PendingValue ?? foundName?.PendingValue ?? pending.HotelName,
            nameFact is null ? null : ToFactIdentity(nameFact, metadata),
            nameEvidence?.EvidenceValue ?? foundName?.EvidenceValue,
            match is not null
                ? NameMatchKindText(match.NameMatchKind)
                : foundName is not null
                    ? NameMatchKindText(foundName.MatchKind)
                    : "未找到相同名称",
            nameEvidence is not null || foundName is not null));

        var coordinateEvidence = match?.CoordinateEvidence;
        var coordinateFact = coordinateEvidence is null
            ? nearestCoordinate?.Fact
            : FindFact(facts, coordinateEvidence.Source, coordinateEvidence.HotelId);
        var coordinateDistance = coordinateEvidence?.DistanceMeters
            ?? nearestCoordinate?.DistanceMeters;
        rows.Add(new EvidenceRowView(
            "经纬度",
            true,
            CoordinateValue(pending),
            coordinateFact is null ? null : ToFactIdentity(coordinateFact, metadata),
            coordinateFact is null ? null : CoordinateValue(coordinateFact),
            coordinateDistance.HasValue
                ? $"{coordinateDistance:0.0} 米 / 阈值 {matchDistanceMeters} 米"
                : "没有可比较的经纬度",
            coordinateDistance.HasValue && coordinateDistance <= matchDistanceMeters,
            coordinateDistance));

        if (match?.StarEvidence is not null)
        {
            rows.Add(ToOptionalEvidenceRow("星级", match.StarEvidence, facts, "数值相同", metadata));
        }
        if (match?.AddressEvidence is not null)
        {
            rows.Add(ToOptionalEvidenceRow("地址", match.AddressEvidence, facts, "标准化后相同", metadata));
        }
        if (match?.PhoneEvidence is not null)
        {
            rows.Add(ToOptionalEvidenceRow("电话", match.PhoneEvidence, facts, "标准化号码相同", metadata));
        }

        return rows;
    }

    static EvidenceRowView ToOptionalEvidenceRow(
        string field,
        HotelMatchEvidence evidence,
        IReadOnlyCollection<HotelMatchFact> facts,
        string comparison,
        IReadOnlyDictionary<string, FactMetadata> metadata)
    {
        var fact = FindFact(facts, evidence.Source, evidence.HotelId);
        return new EvidenceRowView(
            field,
            false,
            evidence.PendingValue,
            fact is null ? null : ToFactIdentity(fact, metadata),
            evidence.EvidenceValue,
            comparison,
            true);
    }

    NameEvidenceInfo? FindNameEvidence(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts)
    {
        return (
            from pendingName in Names(pending)
            from fact in facts
            from factName in Names(fact)
            let result = _nameMatcher.Match(pendingName, factName)
            where result.IsMatch
            orderby NameRank(result.Kind) descending, fact.Source, fact.HotelId
            select new NameEvidenceInfo(
                fact,
                pendingName,
                factName,
                result.Kind))
            .FirstOrDefault();
    }

    static CoordinateEvidenceInfo? FindNearestCoordinate(
        HotelMatchFact pending,
        IReadOnlyCollection<HotelMatchFact> facts)
    {
        if (!pending.Latitude.HasValue || !pending.Longitude.HasValue)
        {
            return null;
        }

        return facts
            .Where(fact => fact.Latitude.HasValue && fact.Longitude.HasValue)
            .Select(fact => new CoordinateEvidenceInfo(
                fact,
                DistanceMeters(
                    pending.Latitude.Value,
                    pending.Longitude.Value,
                    fact.Latitude!.Value,
                    fact.Longitude!.Value)))
            .OrderBy(item => item.DistanceMeters)
            .ThenBy(item => item.Fact.Source)
            .ThenBy(item => item.Fact.HotelId)
            .FirstOrDefault();
    }

    async Task<IReadOnlyDictionary<string, FactMetadata>> LoadFactMetadataAsync(
        HotelMatchingWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var facts = new[]
            {
                workItem.PendingSupplier
            }
            .Concat(workItem.NearbyOtaHotels)
            .Concat(workItem.NearbySupplierHotels)
            .Concat(workItem.OtaLinkedSupplierHotels)
            .DistinctBy(fact => (fact.Source, fact.HotelId))
            .ToArray();
        var otaIds = facts
            .Where(fact => fact.Source == HotelMatchFactSource.Ota)
            .Select(fact => fact.HotelId)
            .ToArray();
        var supplierIds = facts
            .Where(fact => fact.Source == HotelMatchFactSource.Supplier)
            .Select(fact => fact.HotelId)
            .ToArray();

        var ota = await _dbContext.OTAHotelInfos
            .AsNoTracking()
            .Where(row => otaIds.Contains(row.HotelID))
            .Select(row => new
            {
                row.HotelID,
                row.CountryCode,
                row.CountryName,
                row.RegionLevel1Code,
                row.RegionLevel1Name
            })
            .ToArrayAsync(cancellationToken);
        var suppliers = await _dbContext.SupHotelInfos
            .AsNoTracking()
            .Where(row => supplierIds.Contains(row.SupHotelId))
            .Select(row => new
            {
                row.SupHotelId,
                row.CountryCode,
                row.CountryName,
                row.RegionLevel1Code,
                row.RegionLevel1Name
            })
            .ToArrayAsync(cancellationToken);

        var result = new Dictionary<string, FactMetadata>(StringComparer.Ordinal);
        foreach (var row in ota)
        {
            result[FactKey(HotelMatchFactSource.Ota, row.HotelID)] =
                new FactMetadata(
                    row.CountryCode,
                    row.CountryName,
                    row.RegionLevel1Code,
                    row.RegionLevel1Name);
        }
        foreach (var row in suppliers)
        {
            result[FactKey(HotelMatchFactSource.Supplier, row.SupHotelId)] =
                new FactMetadata(
                    row.CountryCode,
                    row.CountryName,
                    row.RegionLevel1Code,
                    row.RegionLevel1Name);
        }
        return result;
    }

    static object ToFactView(
        HotelMatchFact fact,
        IReadOnlyDictionary<string, FactMetadata> metadata)
    {
        metadata.TryGetValue(FactKey(fact.Source, fact.HotelId), out var info);
        return new
        {
            source = fact.Source.ToString(),
            fact.HotelId,
            fact.SupplierId,
            fact.HotelCode,
            fact.MTHotelCode,
            fact.HotelName,
            fact.HotelNameEn,
            countryCode = info?.CountryCode,
            countryName = info?.CountryName,
            regionCode = info?.RegionCode,
            regionName = info?.RegionName,
            fact.Longitude,
            fact.Latitude,
            fact.StarLevel,
            fact.Address,
            fact.Phones
        };
    }

    static EvidenceHotelView ToFactIdentity(
        HotelMatchFact fact,
        IReadOnlyDictionary<string, FactMetadata> metadata)
    {
        metadata.TryGetValue(FactKey(fact.Source, fact.HotelId), out var info);
        return new(
            fact.Source.ToString(),
            fact.HotelId,
            fact.SupplierId,
            fact.HotelCode,
            fact.MTHotelCode,
            fact.HotelName,
            info?.CountryCode,
            info?.CountryName,
            info?.RegionCode,
            info?.RegionName,
            fact.StarLevel,
            fact.Phones);
    }

    static HotelMatchFact? FindFact(
        IEnumerable<HotelMatchFact> facts,
        HotelMatchFactSource source,
        int hotelId)
        => facts.FirstOrDefault(fact => fact.Source == source && fact.HotelId == hotelId);

    static IEnumerable<string> Names(HotelMatchFact fact)
    {
        if (!string.IsNullOrWhiteSpace(fact.HotelName))
        {
            yield return fact.HotelName;
        }
        if (!string.IsNullOrWhiteSpace(fact.HotelNameEn)
            && !string.Equals(fact.HotelName, fact.HotelNameEn, StringComparison.OrdinalIgnoreCase))
        {
            yield return fact.HotelNameEn;
        }
    }

    static int NameRank(HotelNameMatchKind kind)
        => kind switch
        {
            HotelNameMatchKind.SameWordsSameOrder => 3,
            HotelNameMatchKind.SameWordsDifferentOrder => 2,
            HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved => 1,
            _ => 0
        };

    static string NameMatchKindText(HotelNameMatchKind kind)
        => kind switch
        {
            HotelNameMatchKind.SameWordsSameOrder => "名称分词和顺序相同",
            HotelNameMatchKind.SameWordsDifferentOrder => "名称分词相同，顺序不同",
            HotelNameMatchKind.OneSideSameWordsAfterIgnoreWordsRemoved => "去除忽略词后相同",
            _ => "名称不匹配"
        };

    static string CoordinateValue(HotelMatchFact fact)
        => fact.Latitude.HasValue && fact.Longitude.HasValue
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{fact.Latitude:0.#######}, {fact.Longitude:0.#######}")
            : "-";

    static double DistanceMeters(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2)
    {
        const double radius = 6371008.8;
        var lat1 = (double)latitude1 * Math.PI / 180;
        var lat2 = (double)latitude2 * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLon = ((double)longitude2 - (double)longitude1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    static (int Page, int PageSize) NormalizePage(int page, int pageSize)
        => (Math.Max(1, page), Math.Clamp(pageSize, 10, 200));

    static string SupplierKey(int supId, string supplierHotelCode)
        => $"{supId}\u001f{supplierHotelCode}";

    static string FactKey(HotelMatchFactSource source, int hotelId)
        => $"{(byte)source}:{hotelId}";

    static string[] ReadStringArray(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static object? ReadJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    static object? ReadJsonOrText(string? value)
        => ReadJson(value);

    sealed record RecordContext(
        string Kind,
        SupHotelInfoClean? Supplier,
        object Record);

    sealed record NameEvidenceInfo(
        HotelMatchFact Fact,
        string PendingValue,
        string EvidenceValue,
        HotelNameMatchKind MatchKind);

    sealed record CoordinateEvidenceInfo(
        HotelMatchFact Fact,
        double DistanceMeters);

    sealed record EvidenceHotelView(
        string Source,
        int HotelId,
        int? SupplierId,
        string HotelCode,
        string? MTHotelCode,
        string? HotelName,
        string? CountryCode,
        string? CountryName,
        string? RegionCode,
        string? RegionName,
        decimal? StarLevel,
        IReadOnlyList<string> Phones);

    sealed record FactMetadata(
        string? CountryCode,
        string? CountryName,
        string? RegionCode,
        string? RegionName);

    sealed record EvidenceRowView(
        string Field,
        bool Required,
        string? PendingValue,
        EvidenceHotelView? EvidenceHotel,
        string? EvidenceValue,
        string Comparison,
        bool IsMatched,
        double? DistanceMeters = null);
}

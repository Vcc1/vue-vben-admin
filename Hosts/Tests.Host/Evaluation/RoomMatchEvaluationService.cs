using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.MixedData.Entitys;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OTADomain.Matchings;
using RoomMatch.Core.RoomAnalysis;
using RoomMatch.Core.RoomMatch;
using System.Data;
using System.Diagnostics;

namespace Tests.Host.Evaluation;

public sealed class RoomMatchEvaluationService
{
    private const int ReadHotelBatchSize = 100;
    private const int WriteResultBatchSize = 5000;

    private readonly IServiceProvider _serviceProvider;
    private readonly IRoomAnalyzer _roomAnalyzer;
    private readonly ILogger<RoomMatchEvaluationService> _logger;

    public RoomMatchEvaluationService(
        IServiceProvider serviceProvider,
        IRoomAnalyzer roomAnalyzer,
        ILogger<RoomMatchEvaluationService> logger)
    {
        _serviceProvider = serviceProvider;
        _roomAnalyzer = roomAnalyzer;
        _logger = logger;
    }

    public async Task<RoomMatchEvalRun> CreateRunAsync(
        string name,
        string? description,
        long? baselineRunId,
        CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();

        var run = new RoomMatchEvalRun
        {
            Name = name,
            Description = description,
            BaselineRunId = baselineRunId,
            Status = "Running"
        };

        db.RoomMatchEvalRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<RoomMatchEvalRun> ExecuteRunAsync(
        long runId,
        CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();

        var run = await db.RoomMatchEvalRuns.AsTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) throw new InvalidOperationException($"Run {runId} not found");

        try
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("评估运行 {RunId} 开始执行", runId);

            var hotelCodes = await db.OTARoomInfos
                .GroupBy(e => e.MTHotelCode)
                .Select(e => e.Key)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToListAsync(ct);

            run.TotalHotels = hotelCodes.Count;
            _logger.LogInformation("评估运行 {RunId} 共 {Count} 个酒店", runId, hotelCodes.Count);

            var allResults = new List<RoomMatchEvalResult>();

            foreach (var batch in hotelCodes.Chunk(ReadHotelBatchSize))
            {
                var otaRooms = await LoadOTARooms(db, batch, ct);
                if (otaRooms is null) continue;

                var supplierRooms = await LoadSupRooms(db, batch, ct);
                if (supplierRooms is null) continue;

                var mroomDic = otaRooms.GroupBy(e => e.MTHotelCode)
                    .ToDictionary(e => e.Key, e => e.ToList());
                var croomDic = supplierRooms.GroupBy(e => e.MTHotelCode)
                    .ToDictionary(e => e.Key, e => e.ToList());

                foreach (var code in batch)
                {
                    var hotelMRooms = mroomDic.GetValueOrDefault(code) ?? new List<OTARoomInfo>();
                    var hotelCRooms = croomDic.GetValueOrDefault(code) ?? new List<SupRoomInfoView>();

                    if (hotelCRooms.Count == 0) continue;

                    var matchResults = RoomMatch_A.MatchRoom(hotelMRooms, hotelCRooms, _roomAnalyzer);
                    var mRoomById = hotelMRooms.Count > 0
                        ? hotelMRooms.ToDictionary(e => e.Id)
                        : null;

                    foreach (var item in matchResults)
                    {
                        OTARoomInfo? matchedMRoom;
                        RoomMatchStatus status;

                        if (!string.IsNullOrEmpty(item.CRoom.CreateBy))
                        {
                            matchedMRoom = item.CRoom.OTARoomId.HasValue && mRoomById != null
                            && mRoomById.TryGetValue(item.CRoom.OTARoomId.Value, out var manualRoom)
                                ? manualRoom : null;
                            status = RoomMatchStatus.TextMatch;
                        }
                        else
                        {
                            matchedMRoom = item.MRoom;
                            status = item.RoomMatchStatus;
                        }

                        allResults.Add(new RoomMatchEvalResult
                        {
                            RunId = runId,
                            MTHotelCode = item.CRoom.MTHotelCode ?? string.Empty,
                            SupId = item.CRoom.SupId,
                            SupRoomId = item.CRoom.Id,
                            OTARoomId = matchedMRoom?.Id,
                            MatchType = (int)status,
                            MatchScore = status != RoomMatchStatus.NotMatch ? item.Lavel : null,
                            SupRoomNameEN = item.CRoom.SRoomNameEN,
                            SupRoomNameCN = item.CRoom.SRoomNameCN,
                            OTARoomNameEN = matchedMRoom?.RoomNameEN,
                            OTARoomNameCN = matchedMRoom?.RoomNameCN,
                        });
                    }
                }

                if (allResults.Count >= WriteResultBatchSize)
                {
                    await SaveResults(db, allResults, ct);
                    allResults.Clear();
                }
            }

            if (allResults.Count > 0)
            {
                await SaveResults(db, allResults, ct);
                allResults.Clear();
            }

            await ComputeRunStatistics(db, run, ct);

            if (run.BaselineRunId.HasValue)
            {
                await ComputeDiffs(db, run, ct);
            }

            run.Status = "Completed";
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            _logger.LogInformation(
                "评估运行 {RunId} 完成，耗时 {Elapsed}ms，酒店 {Hotels}，供应商房型 {Rooms}，匹配 {Matched}，未匹配 {Unmatched}",
                runId, sw.ElapsedMilliseconds, run.TotalHotels, run.TotalSupplierRooms,
                run.MatchedCount, run.UnmatchedCount);
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogError(ex, "评估运行 {RunId} 失败", runId);
            throw;
        }

        return run;
    }

    private async Task<List<OTARoomInfo>?> LoadOTARooms(
        RoomMappingDbContext db, string[] batch, CancellationToken ct)
    {
        try
        {
            var parameterList = string.Join(",", batch.Select((_, i) => $"@p{i}"));
            var parameters = batch.Select((v, i) => new SqlParameter($"p{i}", v)).ToArray();
            var sql = $"SELECT * FROM OTARoomInfo(NOLock) WHERE MTHotelCode IN ({parameterList}) And Status=1";

            return await db.OTARoomInfos
                .FromSqlRaw(sql, parameters)
                .Select(e => new OTARoomInfo
                {
                    Id = e.Id,
                    RoomNameCN = e.RoomNameCN,
                    RoomNameEN = e.RoomNameEN,
                    BedInfo = e.BedInfo,
                    MTHotelCode = e.MTHotelCode,
                })
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 OTARoomInfo 失败");
            return null;
        }
    }

    private async Task<List<SupRoomInfoView>?> LoadSupRooms(
        RoomMappingDbContext db, string[] batch, CancellationToken ct)
    {
        try
        {
            var dt = new DataTable();
            dt.Columns.Add("Value", typeof(string));
            foreach (var code in batch)
                dt.Rows.Add(code);

            var param = new SqlParameter("@MTHotelCodes", SqlDbType.Structured)
            {
                TypeName = "dbo.StringList",
                Value = dt
            };

            return await db.SupRoomInfoView
                .FromSqlRaw("EXEC sp_GetSupRoomInfoByMTHotelCodes_TVP @MTHotelCodes", param)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 SupRoomInfoView 失败");
            return null;
        }
    }

    private async Task SaveResults(
        RoomMappingDbContext db, List<RoomMatchEvalResult> results, CancellationToken ct)
    {
        db.RoomMatchEvalResults.AddRange(results);
        await db.SaveChangesAsync(ct);
    }

    private async Task ComputeRunStatistics(
        RoomMappingDbContext db, RoomMatchEvalRun run, CancellationToken ct)
    {
        var results = await db.RoomMatchEvalResults
            .Where(r => r.RunId == run.Id)
            .ToListAsync(ct);

        run.TotalSupplierRooms = results.Count;
        run.MatchedCount = results.Count(r => r.OTARoomId.HasValue);
        run.UnmatchedCount = results.Count(r => !r.OTARoomId.HasValue);
        run.FullMatchCount = results.Count(r => r.MatchType == (int)RoomMatchStatus.FullMatch);
        run.TextMatchCount = results.Count(r => r.MatchType == (int)RoomMatchStatus.TextMatch);
        run.NotMatchCount = results.Count(r => r.MatchType == (int)RoomMatchStatus.NotMatch);
    }

    private async Task ComputeDiffs(
        RoomMappingDbContext db, RoomMatchEvalRun run, CancellationToken ct)
    {
        if (!run.BaselineRunId.HasValue) return;

        var baselineResults = await db.RoomMatchEvalResults
            .Where(r => r.RunId == run.BaselineRunId.Value)
            .ToDictionaryAsync(r => r.SupRoomId, ct);

        var currentResults = await db.RoomMatchEvalResults
            .Where(r => r.RunId == run.Id)
            .ToListAsync(ct);

        var diffs = new List<RoomMatchEvalDiff>();

        foreach (var current in currentResults)
        {
            baselineResults.TryGetValue(current.SupRoomId, out var baseline);

            var diff = ClassifyDiff(baseline, current);
            if (diff is not null)
            {
                diff.RunId = run.Id;
                diffs.Add(diff);
            }
        }

        foreach (var baseline in baselineResults.Values)
        {
            if (currentResults.Any(c => c.SupRoomId == baseline.SupRoomId)) continue;

            diffs.Add(new RoomMatchEvalDiff
            {
                RunId = run.Id,
                SupRoomId = baseline.SupRoomId,
                MTHotelCode = baseline.MTHotelCode,
                SupId = baseline.SupId,
                DiffType = "REMOVED",
                Category = "DataChange",
                OldOTARoomId = baseline.OTARoomId,
                OldMatchType = baseline.MatchType,
                OldMatchScore = baseline.MatchScore,
                SupRoomNameEN = baseline.SupRoomNameEN,
                SupRoomNameCN = baseline.SupRoomNameCN,
                OldOTARoomNameEN = baseline.OTARoomNameEN,
                OldOTARoomNameCN = baseline.OTARoomNameCN,
            });
        }

        if (diffs.Count > 0)
        {
            db.RoomMatchEvalDiffs.AddRange(diffs);
            await db.SaveChangesAsync(ct);
        }

        run.DiffNewMatch = diffs.Count(d => d.DiffType == "NEW_MATCH");
        run.DiffLostMatch = diffs.Count(d => d.DiffType == "LOST_MATCH");
        run.DiffReassigned = diffs.Count(d => d.DiffType == "REASSIGNED");
        run.DiffStatusUp = diffs.Count(d => d.DiffType == "STATUS_UP");
        run.DiffStatusDown = diffs.Count(d => d.DiffType == "STATUS_DOWN");
    }

    private static RoomMatchEvalDiff? ClassifyDiff(
        RoomMatchEvalResult? baseline, RoomMatchEvalResult current)
    {
        if (baseline is null)
        {
            if (!current.OTARoomId.HasValue) return null;
            return new RoomMatchEvalDiff
            {
                SupRoomId = current.SupRoomId,
                MTHotelCode = current.MTHotelCode,
                SupId = current.SupId,
                DiffType = "NEW_MATCH",
                Category = "NewMatch",
                NewOTARoomId = current.OTARoomId,
                NewMatchType = current.MatchType,
                NewMatchScore = current.MatchScore,
                SupRoomNameEN = current.SupRoomNameEN,
                SupRoomNameCN = current.SupRoomNameCN,
                NewOTARoomNameEN = current.OTARoomNameEN,
                NewOTARoomNameCN = current.OTARoomNameCN,
            };
        }

        var baselineMatched = baseline.OTARoomId.HasValue;
        var currentMatched = current.OTARoomId.HasValue;

        if (baselineMatched && !currentMatched)
        {
            return new RoomMatchEvalDiff
            {
                SupRoomId = current.SupRoomId,
                MTHotelCode = current.MTHotelCode,
                SupId = current.SupId,
                DiffType = "LOST_MATCH",
                Category = "LostMatch",
                OldOTARoomId = baseline.OTARoomId,
                OldMatchType = baseline.MatchType,
                OldMatchScore = baseline.MatchScore,
                SupRoomNameEN = current.SupRoomNameEN,
                SupRoomNameCN = current.SupRoomNameCN,
                OldOTARoomNameEN = baseline.OTARoomNameEN,
                OldOTARoomNameCN = baseline.OTARoomNameCN,
            };
        }

        if (baselineMatched && currentMatched)
        {
            if (baseline.OTARoomId != current.OTARoomId)
            {
                return new RoomMatchEvalDiff
                {
                    SupRoomId = current.SupRoomId,
                    MTHotelCode = current.MTHotelCode,
                    SupId = current.SupId,
                    DiffType = "REASSIGNED",
                    Category = "Reassigned",
                    OldOTARoomId = baseline.OTARoomId,
                    NewOTARoomId = current.OTARoomId,
                    OldMatchType = baseline.MatchType,
                    NewMatchType = current.MatchType,
                    OldMatchScore = baseline.MatchScore,
                    NewMatchScore = current.MatchScore,
                    SupRoomNameEN = current.SupRoomNameEN,
                    SupRoomNameCN = current.SupRoomNameCN,
                    OldOTARoomNameEN = baseline.OTARoomNameEN,
                    OldOTARoomNameCN = baseline.OTARoomNameCN,
                    NewOTARoomNameEN = current.OTARoomNameEN,
                    NewOTARoomNameCN = current.OTARoomNameCN,
                };
            }

            if (baseline.MatchType != current.MatchType)
            {
                var isUpgrade = current.MatchType > baseline.MatchType;
                return new RoomMatchEvalDiff
                {
                    SupRoomId = current.SupRoomId,
                    MTHotelCode = current.MTHotelCode,
                    SupId = current.SupId,
                    DiffType = isUpgrade ? "STATUS_UP" : "STATUS_DOWN",
                    Category = isUpgrade ? "StatusUpgrade" : "StatusDowngrade",
                    OldOTARoomId = baseline.OTARoomId,
                    NewOTARoomId = current.OTARoomId,
                    OldMatchType = baseline.MatchType,
                    NewMatchType = current.MatchType,
                    OldMatchScore = baseline.MatchScore,
                    NewMatchScore = current.MatchScore,
                    SupRoomNameEN = current.SupRoomNameEN,
                    SupRoomNameCN = current.SupRoomNameCN,
                    OldOTARoomNameEN = baseline.OTARoomNameEN,
                    OldOTARoomNameCN = baseline.OTARoomNameCN,
                    NewOTARoomNameEN = current.OTARoomNameEN,
                    NewOTARoomNameCN = current.OTARoomNameCN,
                };
            }
        }

        return null;
    }

    public async Task<RoomMatchEvalRun?> GetRunAsync(long runId, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        return await db.RoomMatchEvalRuns.FindAsync([runId], ct);
    }

    public async Task<List<RoomMatchEvalRun>> ListRunsAsync(int take = 50, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        return await db.RoomMatchEvalRuns
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<RoomMatchEvalDiff>> GetDiffsAsync(
        long runId, string? diffType, int take = 1000, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();

        var query = db.RoomMatchEvalDiffs.Where(d => d.RunId == runId);
        if (!string.IsNullOrWhiteSpace(diffType))
            query = query.Where(d => d.DiffType == diffType);

        return await query.Take(take).ToListAsync(ct);
    }

    public async Task<List<RoomMatchEvalResult>> GetResultsAsync(
        long runId, int take = 1000, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();

        return await db.RoomMatchEvalResults
            .Where(r => r.RunId == runId)
            .Take(take)
            .ToListAsync(ct);
    }
}

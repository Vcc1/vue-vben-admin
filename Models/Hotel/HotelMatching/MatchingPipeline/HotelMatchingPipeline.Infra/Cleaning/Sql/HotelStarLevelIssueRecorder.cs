using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using HotelMatchingPipeline.App.Contracts;
using HotelMatchingPipeline.Domain.Hotels;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelMatchingPipeline.Infra.Sql;

public sealed class HotelStarLevelIssueRecorder : IHotelStarLevelIssueRecorder
{
    const int FlushBatchSize = 500;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    readonly IServiceScopeFactory _scopeFactory;
    readonly ConcurrentDictionary<IssueKey, PendingIssue> _pending = new();
    readonly ConcurrentDictionary<IssueKey, byte> _persisted = new();
    readonly SemaphoreSlim _initializationLock = new(1, 1);
    bool _initialized;

    public HotelStarLevelIssueRecorder(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Report(HotelDataType dataType, int? supplierId, int? sourceId, string? rawValue, string reason)
    {
        var normalizedRawValue = rawValue is null
            ? "<NULL>"
            : string.IsNullOrWhiteSpace(rawValue) ? "<EMPTY>" : rawValue.Trim();
        var key = new IssueKey((byte)dataType, supplierId ?? 0, normalizedRawValue);
        if (_persisted.ContainsKey(key))
        {
            return;
        }

        _pending.TryAdd(key, new PendingIssue(
            key.SourceType,
            key.SupplierId,
            sourceId ?? 0,
            key.RawStarLevel,
            reason,
            DateTime.Now));
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var candidates = _pending
            .Where(item => !_persisted.ContainsKey(item.Key))
            .Select(item => item.Value)
            .ToArray();

        foreach (var batch in candidates.Chunk(FlushBatchSize))
        {
            await MergeAsync(batch, cancellationToken);
            foreach (var issue in batch)
            {
                var key = new IssueKey(issue.SourceType, issue.SupplierId, issue.RawStarLevel);
                _persisted.TryAdd(key, 0);
                _pending.TryRemove(key, out _);
            }
        }
    }

    async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
            var existing = await database.HotelStarLevelIssues
                .Select(issue => new { issue.SourceType, issue.SupplierId, issue.RawStarLevel })
                .ToListAsync(cancellationToken);
            foreach (var issue in existing)
            {
                _persisted.TryAdd(new IssueKey(issue.SourceType, issue.SupplierId, issue.RawStarLevel), 0);
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    async Task MergeAsync(PendingIssue[] issues, CancellationToken cancellationToken)
    {
        if (issues.Length == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(issues, JsonOptions);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        await database.Database.ExecuteSqlInterpolatedAsync($$"""
            MERGE dbo.HotelStarLevelIssue WITH (HOLDLOCK) AS target
            USING OPENJSON({{json}})
            WITH
            (
                SourceType tinyint '$.sourceType',
                SupplierId int '$.supplierId',
                SourceId int '$.sourceId',
                RawStarLevel nvarchar(100) '$.rawStarLevel',
                FailureReason varchar(100) '$.failureReason',
                FirstSeenTime datetime2(3) '$.firstSeenTime'
            ) AS source
            ON target.SourceType = source.SourceType
               AND target.SupplierId = source.SupplierId
               AND target.RawStarLevel = source.RawStarLevel
            WHEN NOT MATCHED THEN
                INSERT (SourceType, SupplierId, SourceId, RawStarLevel, FailureReason, FirstSeenTime)
                VALUES (source.SourceType, source.SupplierId, source.SourceId, source.RawStarLevel, source.FailureReason, source.FirstSeenTime);
            """, cancellationToken);
    }

    readonly record struct IssueKey(byte SourceType, int SupplierId, string RawStarLevel);

    sealed record PendingIssue(
        byte SourceType,
        int SupplierId,
        int SourceId,
        string RawStarLevel,
        string FailureReason,
        DateTime FirstSeenTime);
}

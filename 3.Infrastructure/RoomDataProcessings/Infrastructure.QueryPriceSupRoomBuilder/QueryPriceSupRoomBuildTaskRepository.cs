using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using OTADomain.RoomDataProcessings;
using QueryPriceSupRoomBuilder;

namespace Infrastructure.QueryPriceSupRoomBuilder;

public sealed class QueryPriceSupRoomBuildTaskRepository : IQueryPriceSupRoomBuildTaskRepository
{
    readonly RoomMappingDbContext _dbContext;

    public QueryPriceSupRoomBuildTaskRepository(RoomMappingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> UpsertTriggeredAsync(IReadOnlyList<string> mtHotelCodes, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var normalized = mtHotelCodes
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var changed = 0;
        foreach (var code in normalized)
        {
            var task = await _dbContext.QueryPriceSupRoomBuildTasks
                .AsTracking()
                .FirstOrDefaultAsync(x => x.MTHotelCode == code, cancellationToken);

            if (task is null)
            {
                _dbContext.QueryPriceSupRoomBuildTasks.Add(new QueryPriceSupRoomBuildTask
                {
                    MTHotelCode = code,
                    Status = QueryPriceSupRoomBuildTaskStatus.Pending,
                    LastTriggeredAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                changed++;
                continue;
            }

            task.LastTriggeredAt = now;
            task.UpdatedAt = now;
            if (!task.Status.IsCompleted())
            {
                task.Status = QueryPriceSupRoomBuildTaskStatus.Pending;
                task.LastError = null;
            }

            changed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return changed;
    }

    public async Task<IReadOnlyList<QueryPriceSupRoomBuildTaskModel>> ClaimAsync(
        int batchSize,
        string targetSuppliers,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var tasks = await _dbContext.QueryPriceSupRoomBuildTasks
            .AsTracking()
            .Where(x => x.Status == QueryPriceSupRoomBuildTaskStatus.Pending
                || x.Status == QueryPriceSupRoomBuildTaskStatus.RetryWaiting)
            .OrderBy(x => x.LastAttemptAt ?? DateTime.MinValue)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var task in tasks)
        {
            task.Status = QueryPriceSupRoomBuildTaskStatus.Processing;
            task.TargetSuppliers = targetSuppliers;
            task.LastAttemptAt = now;
            task.AttemptCount++;
            task.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return tasks.Select(Map).ToArray();
    }

    public async Task MarkCompletedAsync(int id, int queuedCount, CancellationToken cancellationToken)
    {
        var task = await FindTrackedAsync(id, cancellationToken);
        if (task is null)
        {
            return;
        }

        var now = DateTime.Now;
        task.Status = QueryPriceSupRoomBuildTaskStatus.Completed;
        task.IsRoomBuilt = true;
        task.LastPriceHitAt = now;
        task.LastQueuedAt = now;
        task.LastCompletedAt = now;
        task.LastError = null;
        task.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkNoHitAsync(int id, CancellationToken cancellationToken)
    {
        var task = await FindTrackedAsync(id, cancellationToken);
        if (task is null)
        {
            return;
        }

        task.Status = QueryPriceSupRoomBuildTaskStatus.RetryWaiting;
        task.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRetryOrFailedAsync(
        int id,
        int maxRetryCount,
        string error,
        CancellationToken cancellationToken)
    {
        var task = await FindTrackedAsync(id, cancellationToken);
        if (task is null)
        {
            return;
        }

        task.Status = task.AttemptCount >= maxRetryCount
            ? QueryPriceSupRoomBuildTaskStatus.Failed
            : QueryPriceSupRoomBuildTaskStatus.RetryWaiting;
        task.LastError = Truncate(error, 2000);
        task.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<QueryPriceSupRoomBuildTaskStatus, long>> CountByStatusAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.QueryPriceSupRoomBuildTasks
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.LongCount() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }

    async Task<QueryPriceSupRoomBuildTask?> FindTrackedAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.QueryPriceSupRoomBuildTasks
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    static QueryPriceSupRoomBuildTaskModel Map(QueryPriceSupRoomBuildTask task)
    {
        return new QueryPriceSupRoomBuildTaskModel
        {
            Id = task.Id,
            MTHotelCode = task.MTHotelCode,
            Status = task.Status,
            IsRoomBuilt = task.IsRoomBuilt,
            TargetSuppliers = task.TargetSuppliers,
            AttemptCount = task.AttemptCount
        };
    }

    static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

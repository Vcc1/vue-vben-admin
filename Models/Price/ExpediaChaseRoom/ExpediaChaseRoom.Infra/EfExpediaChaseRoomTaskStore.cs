using Infrastructure.Sql.SupCacheDB;
using Infrastructure.Sql.SupCacheDB.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpediaChaseRoomApp;

public sealed class EfExpediaChaseRoomTaskStore : IExpediaChaseRoomTaskStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfExpediaChaseRoomTaskStore> _logger;

    public EfExpediaChaseRoomTaskStore(
        IServiceScopeFactory scopeFactory,
        ILogger<EfExpediaChaseRoomTaskStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task UnlockAllProcessingTasksAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var items = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .Where(x => x.State == 3)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.State = 0;
            item.CompletedBy = string.Empty;
            item.UpdateTime = DateTime.Now;
        }

        if (items.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("已解锁上次运行遗留的 Expedia 追房任务 {Count} 条。", items.Count);
        }
    }

    public async Task SyncTasksAsync(IReadOnlyList<ExpediaChaseRoomTask> tasks, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var today = DateTime.Today;
        var normalizedTasks = tasks
            .Where(x => x.CheckIn >= today && x.CheckOut > x.CheckIn)
            .GroupBy(x => x.Key)
            .Select(group => group.First())
            .ToList();

        var allRows = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .ToListAsync(cancellationToken);

        var normalizedKeys = normalizedTasks.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 有时数据库中可能存在重复的 key 导致 ToDictionary 抛出异常。
        // 这里按 Key 分组并取每组第一条，避免因重复数据导致异常，同时在检测到重复时记录告警。
        var grouped = allRows.GroupBy(x => CreateKey(x), StringComparer.OrdinalIgnoreCase);
        var duplicateGroups = grouped.Where(g => g.Count() > 1).ToList();
        if (duplicateGroups.Count > 0)
        {
            _logger.LogWarning("检测到 {Count} 条具有相同键的 Expedia 追房任务数据库行，处理时将使用每组的第一条记录。", duplicateGroups.Count);
        }

        var currentRows = grouped.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var task in normalizedTasks)
        {
            if (!currentRows.ContainsKey(task.Key))
            {
                dbContext.ExpediaChaseRoomHotels.Add(new ExpediaChaseRoomHotel
                {
                    PropertyId = task.PropertyId,
                    MTHotelCode = task.MTHotelCode ?? string.Empty,
                    Rooms = task.Rooms,
                    Adults = task.Adults,
                    Priority = 2,
                    CheckIn = task.CheckIn,
                    CheckOut = task.CheckOut,
                    UpdateTime = DateTime.Now,
                    State = 0,
                    CompletedBy = string.Empty
                });
            }
        }


        foreach (var row in allRows.Where(x => x.State == 1 && x.Priority != 9 ))
        {
            row.State = 0;
            row.CompletedBy = string.Empty;
            row.UpdateTime = DateTime.Now;
        }
        foreach (var row in allRows.Where(x => x.Priority != 9 && !normalizedKeys.Contains(CreateKey(x))).ToList())
        {
            dbContext.ExpediaChaseRoomHotels.Remove(row);
        }


        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpediaChaseRoomTask>> LeasePendingTasksAsync(
        int take,
        string leaseOwner,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var rows = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .Where(x => x.State == 0)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.UpdateTime)
            .Take(take)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.State = 3;
            row.CompletedBy = leaseOwner;
            row.UpdateTime = DateTime.Now;
        }

        if (rows.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return rows.Select(MapToTask).ToList();
    }

    public async Task AssignProcessingTaskAsync(int taskId, string completedBy, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var row = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

        if (row is null)
        {
            return;
        }

        row.State = 3;
        row.CompletedBy = completedBy;
        row.UpdateTime = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseTaskAsync(int taskId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var row = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

        if (row is null)
        {
            return;
        }

        row.State = 0;
        row.CompletedBy = string.Empty;
        row.UpdateTime = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAndLogAsync(
        ExpediaChaseRoomTask task,
        string completedBy,
        bool hasPrice,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var row = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == task.Id, cancellationToken);

        if (row is not null)
        {
            row.State = 1;
            row.CompletedBy = completedBy;
            row.UpdateTime = DateTime.Now;
        }

        dbContext.ExpediaChaseRoomHotelsLogs.Add(new ExpediaChaseRoomHotelsLog
        {
            HotelTaskId = task.Id,
            LogTime = DateTime.Now,
            MTHotelCode = task.MTHotelCode ?? string.Empty,
            PropertyId = task.PropertyId,
            Rooms = task.Rooms,
            Adults = task.Adults,
            CheckIn = task.CheckIn,
            CheckOut = task.CheckOut,
            Priority = task.Priority,
            CompletedBy = completedBy,
            IsTherePrice = hasPrice
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkOfflineAsync(ExpediaChaseRoomTask task, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupCacheDbContext>();
        var exists = await dbContext.OffLineHotels
            .AnyAsync(x => x.MTHotelCode == (task.MTHotelCode ?? string.Empty), cancellationToken);

        if (!exists)
        {
            dbContext.OffLineHotels.Add(new OffLineHotel
            {
                MTHotelCode = task.MTHotelCode ?? string.Empty,
                SHotelCode = task.PropertyId,
                CreateTime = DateTime.Now
            });
        }

        var row = await dbContext.ExpediaChaseRoomHotels
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == task.Id, cancellationToken);
        if (row is not null)
        {
            dbContext.ExpediaChaseRoomHotels.Remove(row);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ExpediaChaseRoomTask MapToTask(ExpediaChaseRoomHotel row)
    {
        return new ExpediaChaseRoomTask
        {
            Id = row.Id,
            PropertyId = row.PropertyId,
            MTHotelCode = row.MTHotelCode,
            CheckIn = row.CheckIn,
            CheckOut = row.CheckOut,
            Rooms = row.Rooms,
            Adults = row.Adults,
            Priority = row.Priority
        };
    }

    private static string CreateKey(ExpediaChaseRoomHotel row)
    {
        return $"{row.PropertyId}|{row.CheckIn:yyyy-MM-dd}|{row.CheckOut:yyyy-MM-dd}|{row.Rooms}|{row.Adults}";
    }
}

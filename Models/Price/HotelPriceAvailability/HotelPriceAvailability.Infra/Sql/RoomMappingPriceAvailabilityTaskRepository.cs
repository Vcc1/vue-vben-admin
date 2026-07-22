using HotelPriceAvailability.App.Contracts;
using HotelPriceAvailability.Domain;
using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelPriceAvailability.Infra.Sql;

public sealed class RoomMappingPriceAvailabilityTaskRepository : IPriceAvailabilityTaskRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RoomMappingPriceAvailabilityTaskRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<PriceCheckTask>> LoadPendingAsync(
        string batchNo,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        var rows = await db.SearchHasPriceHotelTasks
            .FromSqlInterpolated($"SELECT * FROM SearchHasPriceHotelTask WHERE BatchNo = {batchNo} AND (HasPrice <> 1 OR HasPrice IS NULL)")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    public async Task MarkPricedAsync(
        IReadOnlyList<PriceHit> hits,
        CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();
        foreach (var chunk in hits.Chunk(500))
        {
            var byId = chunk.ToDictionary(static hit => hit.TaskId);
            var ids = byId.Keys.ToArray();
            var rows = await db.SearchHasPriceHotelTasks
                .AsTracking()
                .Where(row => ids.Contains(row.Id))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (byId.TryGetValue(row.Id, out var hit))
                {
                    row.HasPrice = true;
                    row.HasPriceTime = hit.PriceTime;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static PriceCheckTask Map(SearchHasPriceHotelTask row) => new(
        row.Id,
        row.SupId,
        row.MTHotelCode,
        row.SHotelCode,
        row.SupFlag,
        row.SContryCode,
        row.SCityCode);
}

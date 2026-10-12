using HotelPriceAvailability.Domain;
using HotelPriceAvailability.Infra.Sql;
using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotelPriceAvailability.Infra.Tests;

public sealed class RoomMappingPriceAvailabilityTaskRepositoryTests
{
    [Fact]
    public async Task Repository_LoadsOnlyRequestedBatchAndMarksHits()
    {
        const string batchA = "batch-a";
        const string batchB = "batch-b";
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RoomMappingDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new RoomMappingDbContext(options))
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TABLE SearchHasPriceHotelTask (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MTHotelCode TEXT NULL,
                    SHotelCode TEXT NULL,
                    SContryCode TEXT NULL,
                    ChainName TEXT NULL,
                    HasPrice INTEGER NULL,
                    CreateTime TEXT NOT NULL,
                    State INTEGER NOT NULL,
                    SupId INTEGER NOT NULL,
                    SCityCode TEXT NULL,
                    HasPriceTime TEXT NULL,
                    MinPrice TEXT NULL,
                    SupFlag TEXT NULL,
                    BatchNo TEXT NULL
                )
                """);
            var first = new SearchHasPriceHotelTask
            {
                SupId = 51,
                MTHotelCode = "MT-1",
                HasPrice = null
            };
            var second = new SearchHasPriceHotelTask
            {
                SupId = 51,
                MTHotelCode = "MT-2",
                HasPrice = null
            };
            setup.SearchHasPriceHotelTasks.AddRange(first, second);
            await setup.SaveChangesAsync();
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE SearchHasPriceHotelTask SET BatchNo = {batchA} WHERE Id = {first.Id}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE SearchHasPriceHotelTask SET BatchNo = {batchB} WHERE Id = {second.Id}");
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => new RoomMappingDbContext(options));
        await using var provider = services.BuildServiceProvider();
        var repository = new RoomMappingPriceAvailabilityTaskRepository(
            provider.GetRequiredService<IServiceScopeFactory>());

        var tasks = await repository.LoadPendingAsync("batch-a", CancellationToken.None);
        Assert.Single(tasks);
        Assert.Equal("MT-1", tasks[0].MTHotelCode);

        var combination = new PriceSearchCombination(
            new PriceSearchWindow(new DateOnly(2026, 7, 22), new DateOnly(2026, 7, 23), 0, 0),
            1,
            0,
            0);
        await repository.MarkPricedAsync([new PriceHit(tasks[0].Id, combination)], CancellationToken.None);

        await using var verify = new RoomMappingDbContext(options);
        var row = await verify.SearchHasPriceHotelTasks.AsNoTracking().SingleAsync(x => x.Id == tasks[0].Id);
        Assert.True(row.HasPrice);
        Assert.Equal("2026-07-22~2026-07-23", row.HasPriceTime);
    }
}

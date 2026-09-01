using Backoffice.App.HotelPriceComparison;
using Backoffice.Infra.HotelPriceComparison;
using Infrastructure.Sql.MengTuYaoChi;
using Infrastructure.Sql.MengTuYaoChi.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backoffice.Infra.Tests;

public sealed class MengtuYaoChiRunHistoryRepositoryTests
{
    [Fact]
    public async Task ListAsyncFiltersInclusiveDatesAndUsesDeterministicDescendingOrder()
    {
        await using var context = CreateContext();
        context.HotelPriceComparisonRunHistories.AddRange(
            CreateRow(1, new DateOnly(2026, 8, 1), new DateTime(2026, 8, 1, 9, 0, 0)),
            CreateRow(2, new DateOnly(2026, 8, 2), new DateTime(2026, 8, 2, 9, 0, 0)),
            CreateRow(3, new DateOnly(2026, 8, 2), new DateTime(2026, 8, 2, 10, 0, 0)),
            CreateRow(4, new DateOnly(2026, 8, 3), new DateTime(2026, 8, 3, 9, 0, 0)));
        await context.SaveChangesAsync();

        var repository = new MengtuYaoChiRunHistoryRepository(context);
        var result = await repository.ListAsync(new HotelPriceComparisonRunHistoryQuery(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2)));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal([3L, 2L, 1L], result.Items.Select(item => item.HistoryId));
    }

    [Fact]
    public async Task ListAsyncSupportsOpenEndedDateFilters()
    {
        await using var context = CreateContext();
        context.HotelPriceComparisonRunHistories.AddRange(
            CreateRow(1, new DateOnly(2026, 8, 1)),
            CreateRow(2, new DateOnly(2026, 8, 2)),
            CreateRow(3, new DateOnly(2026, 8, 3)));
        await context.SaveChangesAsync();

        var repository = new MengtuYaoChiRunHistoryRepository(context);
        var fromResult = await repository.ListAsync(new HotelPriceComparisonRunHistoryQuery(
            new DateOnly(2026, 8, 2), null));
        var throughResult = await repository.ListAsync(new HotelPriceComparisonRunHistoryQuery(
            null, new DateOnly(2026, 8, 2)));

        Assert.Equal([3L, 2L], fromResult.Items.Select(item => item.HistoryId));
        Assert.Equal([2L, 1L], throughResult.Items.Select(item => item.HistoryId));
    }

    [Fact]
    public async Task ListAsyncReturnsRequestedPageAndUnpagedTotalCount()
    {
        await using var context = CreateContext();
        for (var day = 1; day <= 5; day++)
        {
            context.HotelPriceComparisonRunHistories.Add(
                CreateRow(day, new DateOnly(2026, 8, day)));
        }
        await context.SaveChangesAsync();

        var repository = new MengtuYaoChiRunHistoryRepository(context);
        var result = await repository.ListAsync(
            new HotelPriceComparisonRunHistoryQuery(null, null, Page: 2, PageSize: 2));

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal([3L, 2L], result.Items.Select(item => item.HistoryId));
    }

    static MengtuYaoChiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MengtuYaoChiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MengtuYaoChiDbContext(options);
    }

    static HotelPriceComparisonRunHistory CreateRow(
        long historyId,
        DateOnly statDate,
        DateTime? runStartTime = null)
    {
        var startTime = runStartTime ?? statDate.ToDateTime(new TimeOnly(9, 0));
        return new HotelPriceComparisonRunHistory
        {
            HistoryId = historyId,
            RunId = Guid.NewGuid(),
            RunStartTime = startTime,
            RunEndTime = startTime.AddMinutes(5),
            StatDate = statDate,
            InsertedTime = startTime.AddMinutes(5)
        };
    }
}

public sealed class HotelPriceComparisonRunHistoryServiceTests
{
    [Fact]
    public async Task ListAsyncRejectsInvalidQueryValuesBeforeCallingRepository()
    {
        var repository = new RecordingRepository();
        var service = new HotelPriceComparisonRunHistoryService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(new HotelPriceComparisonRunHistoryQuery(null, null, Page: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(new HotelPriceComparisonRunHistoryQuery(null, null, PageSize: 0)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListAsync(new HotelPriceComparisonRunHistoryQuery(
                new DateOnly(2026, 8, 2),
                new DateOnly(2026, 8, 1))));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(new HotelPriceComparisonRunHistoryQuery(
                null,
                null,
                Page: int.MaxValue,
                PageSize: 2)));

        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task ListAsyncDoesNotCapPageSize()
    {
        var repository = new RecordingRepository();
        var service = new HotelPriceComparisonRunHistoryService(repository);

        await service.ListAsync(new HotelPriceComparisonRunHistoryQuery(
            null,
            null,
            Page: 1,
            PageSize: int.MaxValue));

        Assert.Equal(1, repository.CallCount);
    }

    sealed class RecordingRepository : IHotelPriceComparisonRunHistoryRepository
    {
        public int CallCount { get; private set; }

        public Task<HotelPriceComparisonRunHistoryPage> ListAsync(
            HotelPriceComparisonRunHistoryQuery query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new HotelPriceComparisonRunHistoryPage
            {
                Page = query.Page,
                PageSize = query.PageSize
            });
        }
    }
}

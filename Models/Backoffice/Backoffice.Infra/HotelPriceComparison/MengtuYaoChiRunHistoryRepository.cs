using Backoffice.App.HotelPriceComparison;
using Infrastructure.Sql.MengTuYaoChi;
using Microsoft.EntityFrameworkCore;

namespace Backoffice.Infra.HotelPriceComparison;

public sealed class MengtuYaoChiRunHistoryRepository(MengtuYaoChiDbContext dbContext)
    : IHotelPriceComparisonRunHistoryRepository
{
    public async Task<HotelPriceComparisonRunHistoryPage> ListAsync(
        HotelPriceComparisonRunHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset < 0 || offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "The requested page offset is invalid.");
        }

        var rows = dbContext.HotelPriceComparisonRunHistories.AsNoTracking();
        if (query.StartDate.HasValue)
        {
            rows = rows.Where(row => row.StatDate >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            rows = rows.Where(row => row.StatDate <= query.EndDate.Value);
        }

        var totalCount = await rows.LongCountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(row => row.StatDate)
            .ThenByDescending(row => row.RunStartTime)
            .ThenByDescending(row => row.HistoryId)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(row => new HotelPriceComparisonRunHistoryItem
            {
                HistoryId = row.HistoryId,
                RunId = row.RunId,
                RunStartTime = row.RunStartTime,
                RunEndTime = row.RunEndTime,
                StatDate = row.StatDate,
                RecordCount = row.RecordCount,
                HotelCount = row.HotelCount,
                ErrorRecordCount = row.ErrorRecordCount,
                EanepsPairCount = row.EanepsPairCount,
                EanepsHigherPriceCount = row.EanepsHigherPriceCount,
                EanepsFlatPriceCount = row.EanepsFlatPriceCount,
                EanepsAdvantagePriceCount = row.EanepsAdvantagePriceCount,
                EpsHigherPct = row.EpsHigherPct,
                EpsFlatPct = row.EpsFlatPct,
                EpsAdvantagePct = row.EpsAdvantagePct,
                EanbizPairCount = row.EanbizPairCount,
                EanbizHigherPriceCount = row.EanbizHigherPriceCount,
                EanbizFlatPriceCount = row.EanbizFlatPriceCount,
                EanbizAdvantagePriceCount = row.EanbizAdvantagePriceCount,
                BizHigherPct = row.BizHigherPct,
                BizFlatPct = row.BizFlatPct,
                BizAdvantagePct = row.BizAdvantagePct,
                AccountPairCount = row.AccountPairCount,
                AccountHigherCount = row.AccountHigherCount,
                AccountFlatCount = row.AccountFlatCount,
                AccountAdvantageCount = row.AccountAdvantageCount,
                AccountEpsHigherPct = row.AccountEpsHigherPct,
                AccountFlatPct = row.AccountFlatPct,
                AccountEpsAdvantagePct = row.AccountEpsAdvantagePct,
                InsertedTime = row.InsertedTime
            })
            .ToListAsync(cancellationToken);

        return new HotelPriceComparisonRunHistoryPage
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}

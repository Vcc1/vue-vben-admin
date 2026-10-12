using System.ComponentModel;

namespace Backoffice.App.HotelPriceComparison;

[Description("酒店价格对比任务的分页运行历史。")]
public sealed class HotelPriceComparisonRunHistoryPage
{
    [Description("当前页的价格对比运行历史记录。")]
    public IReadOnlyList<HotelPriceComparisonRunHistoryItem> Items { get; init; } = [];

    [Description("当前页码，从 1 开始。")]
    public int Page { get; init; }

    [Description("每页请求的记录数。")]
    public int PageSize { get; init; }

    [Description("应用日期条件后、分页前的记录总数。")]
    public long TotalCount { get; init; }
}

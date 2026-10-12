using System.ComponentModel;

namespace Backoffice.App.HotelPriceComparison;

[Description("一次酒店价格对比任务的运行结果汇总。")]
public sealed class HotelPriceComparisonRunHistoryItem
{
    [Description("运行历史记录主键。")]
    public long HistoryId { get; init; }

    [Description("价格对比任务的唯一运行标识。")]
    public Guid RunId { get; init; }

    [Description("价格对比任务开始时间。")]
    public DateTime RunStartTime { get; init; }

    [Description("价格对比任务结束时间。")]
    public DateTime RunEndTime { get; init; }

    [Description("业务统计日期，也是开始、结束日期参数筛选的字段。")]
    public DateOnly StatDate { get; init; }

    [Description("本次运行处理的价格对比记录总数。")]
    public long RecordCount { get; init; }

    [Description("本次运行涉及的酒店总数。")]
    public long HotelCount { get; init; }

    [Description("本次运行处理失败或数据异常的记录数。")]
    public long ErrorRecordCount { get; init; }

    [Description("EAN EPS 口径下参与价格对比的配对数。")]
    public long EanepsPairCount { get; init; }

    [Description("EAN EPS 价格高于对比方的配对数。")]
    public long EanepsHigherPriceCount { get; init; }

    [Description("EAN EPS 与对比方价格持平的配对数。")]
    public long EanepsFlatPriceCount { get; init; }

    [Description("EAN EPS 价格优于对比方的配对数。")]
    public long EanepsAdvantagePriceCount { get; init; }

    [Description("EAN EPS 高价配对占比。")]
    public decimal? EpsHigherPct { get; init; }

    [Description("EAN EPS 持平配对占比。")]
    public decimal? EpsFlatPct { get; init; }

    [Description("EAN EPS 优势价配对占比。")]
    public decimal? EpsAdvantagePct { get; init; }

    [Description("EAN BIZ 口径下参与价格对比的配对数。")]
    public long EanbizPairCount { get; init; }

    [Description("EAN BIZ 价格高于对比方的配对数。")]
    public long EanbizHigherPriceCount { get; init; }

    [Description("EAN BIZ 与对比方价格持平的配对数。")]
    public long EanbizFlatPriceCount { get; init; }

    [Description("EAN BIZ 价格优于对比方的配对数。")]
    public long EanbizAdvantagePriceCount { get; init; }

    [Description("EAN BIZ 高价配对占比。")]
    public decimal? BizHigherPct { get; init; }

    [Description("EAN BIZ 持平配对占比。")]
    public decimal? BizFlatPct { get; init; }

    [Description("EAN BIZ 优势价配对占比。")]
    public decimal? BizAdvantagePct { get; init; }

    [Description("账号维度参与价格对比的配对数。")]
    public long AccountPairCount { get; init; }

    [Description("账号维度的高价配对数。")]
    public long AccountHigherCount { get; init; }

    [Description("账号维度的持平配对数。")]
    public long AccountFlatCount { get; init; }

    [Description("账号维度的优势价配对数。")]
    public long AccountAdvantageCount { get; init; }

    [Description("账号维度 EAN EPS 高价配对占比。")]
    public decimal? AccountEpsHigherPct { get; init; }

    [Description("账号维度持平配对占比。")]
    public decimal? AccountFlatPct { get; init; }

    [Description("账号维度 EAN EPS 优势价配对占比。")]
    public decimal? AccountEpsAdvantagePct { get; init; }

    [Description("汇总记录写入数据库的时间。")]
    public DateTime InsertedTime { get; init; }
}

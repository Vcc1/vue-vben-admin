namespace Infrastructure.HTTP.OTAHotelInfo.Models;

public sealed class PageResult<T>
{
    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPage => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount * 1d / PageSize);

    public List<T> Items { get; set; } = [];
}

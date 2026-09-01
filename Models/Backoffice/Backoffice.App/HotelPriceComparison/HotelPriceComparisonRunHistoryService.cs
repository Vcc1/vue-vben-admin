namespace Backoffice.App.HotelPriceComparison;

public sealed class HotelPriceComparisonRunHistoryService(
    IHotelPriceComparisonRunHistoryRepository repository)
{
    public Task<HotelPriceComparisonRunHistoryPage> ListAsync(
        HotelPriceComparisonRunHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Page <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page), "Page must be greater than zero.");
        }

        if (query.PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query.PageSize), "Page size must be greater than zero.");
        }

        if (query.StartDate.HasValue && query.EndDate.HasValue && query.StartDate > query.EndDate)
        {
            throw new ArgumentException("Start date must be earlier than or equal to end date.", nameof(query));
        }

        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(query.Page), "The requested page offset is too large.");
        }

        return repository.ListAsync(query, cancellationToken);
    }
}

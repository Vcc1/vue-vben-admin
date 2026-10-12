namespace Backoffice.App.HotelPriceComparison;

public interface IHotelPriceComparisonRunHistoryRepository
{
    Task<HotelPriceComparisonRunHistoryPage> ListAsync(
        HotelPriceComparisonRunHistoryQuery query,
        CancellationToken cancellationToken = default);
}

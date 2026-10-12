namespace Backoffice.App.HotelPriceComparison;

public sealed record HotelPriceComparisonRunHistoryQuery(
    DateOnly? StartDate,
    DateOnly? EndDate,
    int Page = 1,
    int PageSize = 20);

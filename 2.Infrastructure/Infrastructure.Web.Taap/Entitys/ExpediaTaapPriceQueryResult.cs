namespace Infrastructure.Web.Taap;

public sealed class ExpediaTaapPriceQueryResult
{
    public required string PropertyId { get; init; }

    public required DateOnly CheckIn { get; init; }

    public required DateOnly CheckOut { get; init; }

    public required string RawContent { get; init; }

    public required string RequestContent { get; init; }
}
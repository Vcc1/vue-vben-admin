namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed record HotelNameWordView(
    string CleanedName,
    IReadOnlyList<string> Words,
    string CompactName,
    IReadOnlyList<HotelNameWordBoundary> Boundaries,
    string NormalizedSource)
{
    public string NoSymbolName => CompactName;
}

namespace HotelMatchingPipeline.Domain.HotelNameMatching;

public sealed record HotelNameWordView(
    string CleanedName,
    IReadOnlyList<string> Words,
    string CompactName,
    IReadOnlyList<HotelNameWordBoundary> Boundaries)
{
    public string NoSymbolName => CompactName;
}

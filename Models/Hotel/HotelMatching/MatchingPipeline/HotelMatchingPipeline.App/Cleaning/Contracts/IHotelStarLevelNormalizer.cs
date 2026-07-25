namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelStarLevelNormalizer
{
    HotelStarLevelNormalization Normalize(int? supplierId, string? rawValue);
}

public readonly record struct HotelStarLevelNormalization(
    bool Success,
    decimal? Value,
    string Reason)
{
    public static HotelStarLevelNormalization Valid(decimal? value)
        => new(true, value, string.Empty);

    public static HotelStarLevelNormalization Invalid(string reason)
        => new(false, null, reason);
}

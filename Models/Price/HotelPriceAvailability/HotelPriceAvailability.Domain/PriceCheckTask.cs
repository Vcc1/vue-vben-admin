namespace HotelPriceAvailability.Domain;

public sealed record PriceCheckTask(
    int Id,
    int SupId,
    string? MTHotelCode,
    string? SHotelCode,
    string? SupplierFlag,
    string? SupplierCountryCode,
    string? SupplierCityCode)
{
    public PriceLookupMode? LookupMode =>
        !string.IsNullOrWhiteSpace(MTHotelCode)
            ? PriceLookupMode.MappedHotel
            : !string.IsNullOrWhiteSpace(SHotelCode)
                ? PriceLookupMode.SupplierHotel
                : null;

    public string? LookupCode => LookupMode switch
    {
        PriceLookupMode.MappedHotel => MTHotelCode,
        PriceLookupMode.SupplierHotel => SHotelCode,
        _ => null
    };
}

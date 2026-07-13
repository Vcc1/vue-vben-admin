namespace HotelMatchingPipeline.Domain.Hotels;

public sealed class Hotel
{
    private Hotel(RawHotelData? rawData, CleanedHotel cleanedData)
    {
        ArgumentNullException.ThrowIfNull(cleanedData);

        RawData = rawData;
        CleanedData = cleanedData;
    }

    public CleanedHotel CleanedData { get; private set; }

    public RawHotelData? RawData { get; }

    public bool HasRawData => RawData is not null;

    public static Hotel FromRaw(RawHotelData rawData, CleanedHotel cleanedData)
    {
        ArgumentNullException.ThrowIfNull(rawData);

        return new Hotel(rawData, cleanedData);
    }

    public static Hotel FromCleaned(CleanedHotel cleanedData)
    {
        return new Hotel(null, cleanedData);
    }

    public void ReplaceCleanedData(CleanedHotel cleanedData)
    {
        ArgumentNullException.ThrowIfNull(cleanedData);

        CleanedData = cleanedData;
    }
}

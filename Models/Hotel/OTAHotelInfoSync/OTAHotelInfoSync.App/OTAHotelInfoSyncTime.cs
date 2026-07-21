namespace OTAHotelInfoSync;

public static class OTAHotelInfoSyncTime
{
    static readonly TimeSpan Utc8Offset = TimeSpan.FromHours(8);

    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Utc8Offset);

    public static DateTime LocalNow => Now.DateTime;
}

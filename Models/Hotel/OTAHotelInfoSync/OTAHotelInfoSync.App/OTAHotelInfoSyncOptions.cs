using System.ComponentModel.DataAnnotations;

namespace OTAHotelInfoSync;

public sealed class OTAHotelInfoSyncOptions
{
    public const string SectionName = "OTAHotelInfoSync";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 60;
}

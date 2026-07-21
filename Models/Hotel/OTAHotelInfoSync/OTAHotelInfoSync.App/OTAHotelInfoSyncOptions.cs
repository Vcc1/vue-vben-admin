using System.ComponentModel.DataAnnotations;

namespace OTAHotelInfoSync;

public sealed class OTAHotelInfoSyncOptions
{
    public const string SectionName = "OTAHotelInfoSync";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 60;

    [Range(1, 3600)]
    public int RetryDelaySeconds { get; set; } = 30;

    [Range(1, 10_000)]
    public int AlertAfterFailures { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int AlertAgentId { get; set; } = 1000026;

    public string[] AlertToUsers { get; set; } = ["zhangwei", "tangwei"];
}

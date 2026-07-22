using System.ComponentModel.DataAnnotations;

namespace HotelMatchingPipeline.App.Configuration;

public sealed class HotelCleaningOptions
{
    public const string SectionName = "HotelCleaning";

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int RegionFenceVersion { get; set; } = 1;

    [Required]
    public string GeoJsonDirectory { get; set; } = @"C:\project\code\mengtu.theforce";

    [Range(1, 100_000)]
    public int BatchSize { get; set; } = 1_000;

    [Range(1, 1_000)]
    public int SaveBatchSize { get; set; } = 100;

    [Range(1, 10_000)]
    public int QueueCapacity { get; set; } = 10_000;

    [Range(1, 64)]
    public int MemoryBudgetGiB { get; set; } = 10;

    [Range(1, 168)]
    public int ChangeWindowHours { get; set; } = 25;
}

public sealed class HotelMatchingOptions
{
    public const string SectionName = "HotelMatching";

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    [Range(1, 10_000)]
    public int BatchSize { get; set; } = 100;

    [Range(1, 60_000)]
    public int IdleDelayMilliseconds { get; set; } = 5_000;

    [Range(100, 10_000)]
    public int RecallRadiusMeters { get; set; } = 3_000;

    [Range(1, 1_000)]
    public int MaxCandidates { get; set; } = 200;
}

using System.ComponentModel.DataAnnotations;

namespace HotelMatchingPipeline.App.Configuration;

public sealed class HotelCleaningOptions
{
    public const string SectionName = "HotelCleaning";
    public const int MaxSaveBatchSize = 1_000;

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int RegionFenceVersion { get; set; } = 1;

    [Required]
    public string GeoJsonDirectory { get; set; } = @"C:\project\code\mengtu.theforce";

    [Range(1, 100_000)]
    public int BatchSize { get; set; } = 20_000;

    [Range(1, MaxSaveBatchSize)]
    public int SaveBatchSize { get; set; } = MaxSaveBatchSize;

    [Range(1, 64)]
    public int CleanParallelism { get; set; } = 8;

    [Range(1, 1_000_000)]
    public int QueueCapacity { get; set; } = 20_000;

    [Range(0, 999_999)]
    public int QueueRefillThreshold { get; set; } = 20_000;

    [Range(0, 3_600)]
    public int QueueRefillDelaySeconds { get; set; } = 120;

    [Range(1, 64)]
    public int MemoryBudgetGiB { get; set; } = 10;

    [Range(1, 168)]
    public int ChangeWindowHours { get; set; } = 25;
}

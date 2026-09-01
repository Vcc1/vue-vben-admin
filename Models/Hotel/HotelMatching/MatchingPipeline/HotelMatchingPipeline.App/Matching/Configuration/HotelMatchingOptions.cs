using System.ComponentModel.DataAnnotations;

namespace HotelMatchingPipeline.App.Configuration;

public sealed class HotelMatchingOptions
{
    public const string SectionName = "HotelMatching";
    public const int MaxQueueCapacity = 500;

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    [Range(1, 10_000)]
    public int BatchSize { get; set; } = 100;

    [Range(1, MaxQueueCapacity)]
    public int QueueCapacity { get; set; } = MaxQueueCapacity;

    [Range(1, MaxQueueCapacity)]
    public int WriteBatchSize { get; set; } = 100;

    [Range(1, 60_000)]
    public int IdleDelayMilliseconds { get; set; } = 5_000;

    [Range(1, 300_000)]
    public int InformationRetryDelayMilliseconds { get; set; } = 2_000;

    [Range(1_000, 300_000)]
    public int InformationSlowLogThresholdMilliseconds { get; set; } = 30_000;

    [Range(1_000, 300_000)]
    public int ProgressLogIntervalMilliseconds { get; set; } = 10_000;

    [Range(1, 64)]
    public int InformationParallelism { get; set; } = 8;

    [Range(100, 10_000)]
    public int RecallRadiusMeters { get; set; } = 3_000;

    [Range(1, 1_000)]
    public int MaxCandidates { get; set; } = 200;

    [Range(1, 1_000)]
    public int MatchDistanceMeters { get; set; } = 60;

    public bool EnableSearchFacts { get; set; }
}

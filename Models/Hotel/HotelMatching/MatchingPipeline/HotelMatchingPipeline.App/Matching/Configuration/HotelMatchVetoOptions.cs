using System.ComponentModel.DataAnnotations;

namespace HotelMatchingPipeline.App.Configuration;

public sealed class HotelMatchVetoOptions
{
    public const string SectionName = "HotelMatchingVeto";

    [Range(1, 3600)]
    public int RetryDelaySeconds { get; set; } = 10;

    [Range(1, 100)]
    public int FastRetryFailureCount { get; set; } = 10;

    [Range(1, 3600)]
    public int FastRetryDelaySeconds { get; set; } = 1;

    [Range(1, 1000)]
    public int SyncBatchSize { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int AlertAgentId { get; set; } = 1000026;

    public string[] AlertToUsers { get; set; } = ["zhangwei"];
}

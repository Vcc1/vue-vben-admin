using System.ComponentModel.DataAnnotations;

namespace OTARoomBuilder;

public sealed class OTARoomBuilderOptions
{
    public int TaskChunkSize { get; set; } = 50;

    public int LastDaysOffset { get; set; } = -32;

    public string? FullSyncCron { get; set; }

    public string DailyCron { get; set; } = "0 30 9 * * *";

    public bool RunFullSyncImmediatelyOnStartup { get; set; } = true;

    public bool RunImmediatelyOnStartup
    {
        get => RunFullSyncImmediatelyOnStartup;
        set => RunFullSyncImmediatelyOnStartup = value;
    }

    public bool EnableIncrementalScan { get; set; } = true;

    public int IncrementalPollIntervalSeconds { get; set; } = 10;

    public int IncrementalBatchSize { get; set; } = 100;

    public int IncrementalStartupLookbackSeconds { get; set; } = 300;

    [Required]
    public string OutputMode { get; set; } = "create";

    public int SqlTimeoutMinutes { get; set; } = 3;

    public string GetFullSyncCron()
    {
        return string.IsNullOrWhiteSpace(FullSyncCron) ? DailyCron : FullSyncCron;
    }
}

public sealed class RoomCreateOptions
{
    [Required]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string ApiToken { get; set; } = string.Empty;
}

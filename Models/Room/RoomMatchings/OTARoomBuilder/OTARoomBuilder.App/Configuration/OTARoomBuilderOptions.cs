using System.ComponentModel.DataAnnotations;

namespace OTARoomBuilder;

public sealed class OTARoomBuilderOptions
{
    static readonly int[] DefaultEnabledSupIds = [51, 32];

    public int TaskChunkSize { get; set; } = 50;

    [Range(1, 100)]
    public int HotelBuildQueueCapacity { get; set; } = 100;

    [Range(1, 100)]
    public int RoomCreateQueueCapacity { get; set; } = 100;

    [Range(1, 100)]
    public int StaticDataTaskConcurrency { get; set; } = 10;

    [Range(1, 100)]
    public int StaticDataQueueCapacity { get; set; } = 100;

    public int LastDaysOffset { get; set; } = -32;

    public string? FullSyncHotelCodes { get; set; }

    public string? EnabledSupIds { get; set; } = "51,32";

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

    public IReadOnlyList<string> GetFullSyncHotelCodeOverrides()
    {
        if (string.IsNullOrWhiteSpace(FullSyncHotelCodes))
        {
            return [];
        }

        return FullSyncHotelCodes
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<int> GetEnabledSupIds()
    {
        if (string.IsNullOrWhiteSpace(EnabledSupIds))
        {
            return DefaultEnabledSupIds;
        }

        var supIds = EnabledSupIds
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => int.TryParse(value, out var supId) ? supId : 0)
            .Where(static supId => supId > 0)
            .Distinct()
            .ToArray();

        return supIds.Length == 0 ? DefaultEnabledSupIds : supIds;
    }
}

public sealed class RoomCreateOptions
{
    [Required]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string ApiToken { get; set; } = string.Empty;
}

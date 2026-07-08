using System.ComponentModel.DataAnnotations;

namespace QueryPriceSupRoomBuilder;

public sealed class RoomBuildTaskOptions
{
    [Required]
    public string Cron { get; set; } = "0 */10 * * * *";

    [Range(1, 10000)]
    public int TriggerBatchSize { get; set; } = 100;

    [Range(0, 525600)]
    public int AttemptCooldownMinutes { get; set; } = 10080;

    [Range(1, 3600)]
    public int IdleDelaySeconds { get; set; } = 5;

    public int[] Suppliers { get; set; } = [51];

    public int[] Persons { get; set; } = [2];

    [Required]
    public string DateWindows { get; set; } = "1:1:59;2:1:59;3:1:59";

    [Range(1, 1000)]
    public int DefaultMaxConcurrency { get; set; } = 10;

    [Range(1, 1000)]
    public int DefaultBatchSize { get; set; } = 1;

    public Dictionary<int, SupplierExecutionOptions> SupConcurrencyOverrides { get; set; } = [];

    public Dictionary<int, int> SupRateLimits { get; set; } = [];

    [Range(0, 100)]
    public int MaxRetryCount { get; set; } = 5;
}

public sealed class SupplierExecutionOptions
{
    [Range(1, 1000)]
    public int MaxConcurrency { get; set; } = 10;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 1;
}

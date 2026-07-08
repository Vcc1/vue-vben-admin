using System.ComponentModel.DataAnnotations;

namespace SupRoomInfoIngestion;

public sealed class SupRoomInfoIngestionOptions
{
    [Range(1, 100_000)]
    public int FetchBatchSize { get; set; } = 1000;

    [Range(1, 100_000)]
    public int WriteBatchSize { get; set; } = 1000;

    [Range(1, 3600)]
    public int IdleDelaySeconds { get; set; } = 2;

    [Range(1, 3600)]
    public int MetricsLogIntervalSeconds { get; set; } = 30;

    [Required]
    public string ListKey { get; set; } = "NewSupRoom";

    [Required]
    public string SetKeyPrefix { get; set; } = "nr_";

    public int[] SetSupIds { get; set; } = [];

    [Range(1, 10_000)]
    public int SetFetchBatchSize { get; set; } = 200;
}

using System.ComponentModel.DataAnnotations;
using VBaseFramework.Core.IOC;

[ConfigSection("OtaRoomInfoCompletion", Reloadable = true, ValidateDataAnnotations = true, ValidateOnStart = true)]
public class OtaRoomInfoCompletionOptions
{
    [Range(1, 10000)]
    public int HotelCodeQueueCapacity { get; set; } = 10_000;

    [Range(1, 10000)]
    public int EanHotelQueueCapacity { get; set; } = 5_000;

    [Range(1, 10000)]
    public int HotelRoomDataQueueCapacity { get; set; } = 5_000;

    [Range(1, 10000)]
    public int ParsedHotelQueueCapacity { get; set; } = 5_000;

    [Range(1, 100)]
    public int EanBatchSize { get; set; } = 100;

    [Range(1, 100)]
    public int EanFetchConcurrency { get; set; } = 4;

    [Range(1, 100)]
    public int LoadHotelRoomDataConcurrency { get; set; } = 8;

    [Range(1, 100)]
    public int ParseHotelConcurrency { get; set; } = 8;

    [Range(1, 10000)]
    public int SaveBatchSize { get; set; } = 200;

    public bool SkipEanFetch { get; set; }
}

namespace HotelPriceAvailability.App.Configuration;

public sealed class HotelPriceAvailabilityOptions
{
    public const string SectionName = "HotelPriceAvailability";

    public string BatchNo { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 100;
    public int MaxConcurrency { get; set; } = 20;
    public int[] Persons { get; set; } = [1, 2, 3, 4];
    public DateWindowOptions[] DateWindows { get; set; } = [];
    public SupplierOverrideOptions[] SupplierOverrides { get; set; } = [];
}

public sealed class DateWindowOptions
{
    public int StayDays { get; set; }
    public int FromDay { get; set; }
    public int ToDay { get; set; }
}

public sealed class SupplierOverrideOptions
{
    public int SupId { get; set; }
    public int MaxConcurrency { get; set; }
    public int BatchSize { get; set; }
    public int RequestsPerSecond { get; set; }
}

public sealed record SupplierExecutionSettings(
    int MaxConcurrency,
    int BatchSize,
    int RequestsPerSecond);

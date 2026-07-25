using System.ComponentModel.DataAnnotations;

namespace HotelMatchingPipeline.App.Configuration;

public sealed class HotelStarLevelOptions
{
    public const string SectionName = "HotelStarLevel";

    [Range(0, 7)]
    public decimal Minimum { get; set; } = 0;

    [Range(0, 7)]
    public decimal Maximum { get; set; } = 7;

    public Dictionary<int, Dictionary<string, decimal?>> SupplierMappings { get; set; } = [];
}

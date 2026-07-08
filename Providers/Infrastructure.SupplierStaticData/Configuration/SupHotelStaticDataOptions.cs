using System.ComponentModel.DataAnnotations;

namespace Infrastructure.SupplierStaticData.Configuration;

public sealed class SupHotelStaticDataOptions
{
    [Required]
    public string Url { get; set; } = string.Empty;
}

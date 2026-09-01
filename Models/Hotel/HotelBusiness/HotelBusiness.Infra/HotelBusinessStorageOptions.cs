using System.ComponentModel.DataAnnotations;

namespace HotelBusiness.Infra;

public sealed class HotelBusinessStorageOptions
{
    public const string SectionName = "HotelBusiness:Storage";

    [Required]
    public string RootPath { get; set; } = "App_Data/hotel-business";
}

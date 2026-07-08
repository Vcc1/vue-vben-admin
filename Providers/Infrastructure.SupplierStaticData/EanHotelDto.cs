using System.Text.Json.Serialization;

namespace Infrastructure.SupplierStaticData;

public sealed class EanHotelDto
{
    [JsonPropertyName("property_id")]
    public string? PropertyId { get; set; }

    public string? Name { get; set; }

    public EanAddress? Address { get; set; }

    public EanRatings? Ratings { get; set; }

    public EanLocation? Location { get; set; }

    public string? Phone { get; set; }

    public EanCategory? Category { get; set; }

    public Dictionary<string, EanRoom>? Rooms { get; set; }
}

public sealed class EanAddress
{
    [JsonPropertyName("line_1")]
    public string? Line1 { get; set; }

    [JsonPropertyName("line_2")]
    public string? Line2 { get; set; }

    public string? City { get; set; }

    [JsonPropertyName("state_province_code")]
    public string? StateProvinceCode { get; set; }

    [JsonPropertyName("state_province_name")]
    public string? StateProvinceName { get; set; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }
}

public sealed class EanRatings
{
    public EanPropertyRating? Property { get; set; }
}

public sealed class EanPropertyRating
{
    public string? Rating { get; set; }

    public string? Type { get; set; }
}

public sealed class EanLocation
{
    public EanCoordinates? Coordinates { get; set; }
}

public sealed class EanCoordinates
{
    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}

public sealed class EanCategory
{
    public string? Id { get; set; }

    public string? Name { get; set; }
}

public sealed class EanRoom
{
    public long Id { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("bed_groups")]
    public Dictionary<string, EanBedGroup>? BedGroups { get; set; }

    public EanOccupancy? Occupancy { get; set; }
}

public sealed class EanBedGroup
{
    public string? Description { get; set; }
}

public sealed class EanOccupancy
{
    [JsonPropertyName("max_allowed")]
    public EanMaxAllowed? MaxAllowed { get; set; }
}

public sealed class EanMaxAllowed
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

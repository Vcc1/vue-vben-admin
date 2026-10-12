using System.Text.Json.Serialization;

namespace Infrastructure.HTTP.OTAHotelInfo.Models;

public sealed class HotelInfoPageReq
{
    public List<string>? MTHotelCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public DateTime? DateTime { get; set; }

    public List<int>? HotelIds { get; set; }

    public DateTime? EndTime { get; set; }

    public int? MinHotelId { get; set; }

    /// <summary>
    /// Page number, starting at 1.
    /// </summary>
    public int PageIndex { get; set; }

    public int PageSize { get; set; }
}

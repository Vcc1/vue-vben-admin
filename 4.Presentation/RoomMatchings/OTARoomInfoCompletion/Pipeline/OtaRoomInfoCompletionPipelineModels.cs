using Infrastructure.Sql.MengTuSup.Entitys;
using Infrastructure.Sql.RoomMapping.Entitys;
using Sup.InterFace.SupBaseApi.EANEPS.Entitys.EanHotelInfo;
using System.Text.Json.Serialization;

public sealed class OtaRoomInfoCompletionWorkItem
{
    public string MTHotelCode { get; set; } = string.Empty;

    public string SHotelCode { get; set; } = string.Empty;

    public int SupId { get; set; }

    public EPSHotelInfoRespone? HotelInfo { get; set; }

    public IReadOnlyList<OTARoomInfo> OtaRooms { get; set; } = Array.Empty<OTARoomInfo>();

    public IReadOnlyList<SupRoomInfo> SupRooms { get; set; } = Array.Empty<SupRoomInfo>();

    public IReadOnlyList<SupStaticRoomInfo> SupStaticRooms { get; set; } = Array.Empty<SupStaticRoomInfo>();

    public List<OTARoomInfoExtension> OTARoomInfoExtensions { get; set; } = new();
}

public sealed class OtaRoomInfoCompletionArea
{
    [JsonPropertyName("squareMeters")]
    public decimal? SquareMeters { get; set; }

    [JsonPropertyName("squareFeet")]
    public decimal? SquareFeet { get; set; }
}

public sealed class OtaRoomInfoCompletionBedInfo
{
    public List<OtaRoomInfoCompletionBedOrGroup> OrGroups { get; set; } = new();
}

public sealed class OtaRoomInfoCompletionBedOrGroup
{
    public List<OtaRoomInfoCompletionBedAndItem> AndItems { get; set; } = new();
}

public sealed class OtaRoomInfoCompletionBedAndItem
{
    public string RawText { get; set; } = string.Empty;

    public string? CountText { get; set; }

    public string? BedSizeType { get; set; }

    public string? BedType { get; set; }
}

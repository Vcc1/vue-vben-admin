namespace RoomMatch.App.Models;

public record HotelRoomMappingInfoV2(
    List<OTARoomInfoDto>? OTARoomInfos,
    List<SupRoomInfoDto>? SupRoomInfos,
    List<RoomMapDto>? RoomMaps
);

public record OTARoomInfoDto(
    int Id,
    string? MTHotelCode,
    string? RoomNameCN,
    string? RoomNameEN,
    byte Person,
    int? RoomQuantity,
    short? FloorMin,
    short? FloorMax,
    short? AreaMin,
    short? AreaMax,
    byte? Smoke,
    DateTime CreateTime,
    int? Status,
    string? Author
);

public record SupRoomInfoDto(
    int Id,
    string? SHotelCode,
    int SupId,
    string? SRoomNameCN,
    string? BedInfoKey,
    string? SRoomCode,
    string? MTHotelCode,
    string? SRoomNameEN,
    DateTime? LastUpdateDate,
    string? BedInfo,
    byte? Status,
    decimal? AcreageFrom,
    decimal? AcreageTo,
    int? SmokeType,
    int? MaxAdt,
    int? MaxChd
);

public record RoomMapDto(
    int Id,
    string? MTHotelCode,
    int SupRoomId,
    int OTARoomId,
    int Status,
    string? CreateBy,
    DateTime? CreateDate,
    int? MatchType
);

public record WeeklyAgentStatDto(
    DateTime StatDate,
    int SupId,
    Guid BatchId,
    DateTime InsertTime,
    int TotalCount,
    int WithMTHotelCodeCount,
    int WithRoomMapCount,
    int? UnmatchedRecentCount
);

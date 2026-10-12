SET NOCOUNT ON;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OTAHotelInfo')
      AND name = N'IX_OTAHotelInfo_CleaningSnapshot'
)
BEGIN
    CREATE INDEX IX_OTAHotelInfo_CleaningSnapshot
        ON dbo.OTAHotelInfo(CleanVersion, RegionFenceVersion, HotelID)
        INCLUDE(SourceDataVersion, CleanTime)
        ON [CACHE];
END;

IF OBJECT_ID(N'dbo.HotelCleaningFailure', N'U') IS NOT NULL
   AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.HotelCleaningFailure')
      AND name = N'IX_HotelCleaningFailure_CleaningSnapshot'
)
BEGIN
    CREATE INDEX IX_HotelCleaningFailure_CleaningSnapshot
        ON dbo.HotelCleaningFailure
        (
            SourceType,
            CleanVersion,
            RegionBoundaryVersion,
            DataId
        )
        ON [CACHE];
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
      AND name = N'IX_SupHotelInfo_CleaningSnapshot'
)
BEGIN
    CREATE INDEX IX_SupHotelInfo_CleaningSnapshot
        ON dbo.SupHotelInfo(CleanVersion, RegionFenceVersion, SupHotelId)
        INCLUDE(SourceDataVersion, CleanTime)
        ON [CACHE];
END;

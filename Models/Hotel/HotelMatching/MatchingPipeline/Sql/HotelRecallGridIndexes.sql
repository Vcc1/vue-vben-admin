/*
  Hotel matching recall grid indexes.

  Purpose:
  - Non-CN/TW/HK/MO hotel matching recalls candidates by RecallGridId.
  - These indexes make 100m grid recall use bigint range seeks instead of geography distance scans.
  - This script is idempotent. Run it on the RoomMapping database.
*/

SET NOCOUNT ON;

PRINT N'Current RecallGridId columns and indexes';

SELECT
    table_name = OBJECT_SCHEMA_NAME(column_definition.object_id) + N'.' + OBJECT_NAME(column_definition.object_id),
    column_definition.name,
    column_definition.is_computed,
    column_definition.is_persisted,
    computed_definition = computed_definition.definition
FROM sys.columns AS column_definition
LEFT JOIN sys.computed_columns AS computed_definition
    ON computed_definition.object_id = column_definition.object_id
   AND computed_definition.column_id = column_definition.column_id
WHERE column_definition.object_id IN (OBJECT_ID(N'dbo.OTAHotelInfo'), OBJECT_ID(N'dbo.SupHotelInfo'))
  AND column_definition.name = N'RecallGridId';

SELECT
    table_name = OBJECT_SCHEMA_NAME(index_definition.object_id) + N'.' + OBJECT_NAME(index_definition.object_id),
    index_definition.name,
    index_definition.type_desc,
    index_definition.has_filter,
    index_definition.filter_definition,
    key_columns = STRING_AGG(
        CASE WHEN index_column.is_included_column = 0 THEN column_definition.name END,
        N', '
    ) WITHIN GROUP (ORDER BY index_column.key_ordinal),
    include_columns = STRING_AGG(
        CASE WHEN index_column.is_included_column = 1 THEN column_definition.name END,
        N', '
    ) WITHIN GROUP (ORDER BY index_column.index_column_id)
FROM sys.indexes AS index_definition
INNER JOIN sys.index_columns AS index_column
    ON index_column.object_id = index_definition.object_id
   AND index_column.index_id = index_definition.index_id
INNER JOIN sys.columns AS column_definition
    ON column_definition.object_id = index_column.object_id
   AND column_definition.column_id = index_column.column_id
WHERE index_definition.object_id IN (OBJECT_ID(N'dbo.OTAHotelInfo'), OBJECT_ID(N'dbo.SupHotelInfo'))
  AND index_definition.name IN (N'IX_OTAHotelInfo_RecallGridId', N'IX_SupHotelInfo_RecallGridId')
GROUP BY
    index_definition.object_id,
    index_definition.name,
    index_definition.type_desc,
    index_definition.has_filter,
    index_definition.filter_definition;
GO

IF OBJECT_ID(N'dbo.OTAHotelInfo', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.OTAHotelInfo', N'RecallGridId') IS NULL
BEGIN
    ALTER TABLE dbo.OTAHotelInfo ADD RecallGridId AS
    (
        CASE
            WHEN Latitude >= (-90.0)
             AND Latitude <= (90.0)
             AND Longitude >= (-180.0)
             AND Longitude <= (180.0)
                THEN CONVERT(bigint, FLOOR((Latitude + (90.0)) * (2000.0)))
                   * CONVERT(bigint, 720001)
                   + CONVERT(bigint, FLOOR((Longitude + (180.0)) * (2000.0)))
        END
    );
END;
GO

IF OBJECT_ID(N'dbo.SupHotelInfo', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SupHotelInfo', N'RecallGridId') IS NULL
BEGIN
    ALTER TABLE dbo.SupHotelInfo ADD RecallGridId AS
    (
        CASE
            WHEN Latitude >= (-90.0)
             AND Latitude <= (90.0)
             AND Longitude >= (-180.0)
             AND Longitude <= (180.0)
                THEN CONVERT(bigint, FLOOR((Latitude + (90.0)) * (2000.0)))
                   * CONVERT(bigint, 720001)
                   + CONVERT(bigint, FLOOR((Longitude + (180.0)) * (2000.0)))
        END
    );
END;
GO

IF OBJECT_ID(N'dbo.OTAHotelInfo', N'U') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.OTAHotelInfo')
          AND name = N'IX_OTAHotelInfo_RecallGridId'
    )
        DROP INDEX IX_OTAHotelInfo_RecallGridId ON dbo.OTAHotelInfo;

    CREATE NONCLUSTERED INDEX IX_OTAHotelInfo_RecallGridId
    ON dbo.OTAHotelInfo(RecallGridId ASC, HotelID ASC)
    INCLUDE
    (
        Estate,
        CleanStatus,
        DataQualityStatus,
        MTHotelCode,
        HotelName,
        HotelEnName,
        Longitude,
        Latitude,
        StarLevel,
        Address,
        RegionLevel1Code,
        CountryCode,
        CountryName,
        CityCode,
        CityName,
        PostCode,
        Tel,
        Fax,
        Email,
        CoordType,
        RegionLevel1Name,
        RegionLevel2Code,
        RegionLevel2Name,
        RegionLevel3Code,
        RegionLevel3Name,
        RegionResolveStatus,
        DataQualityFlags
    );
END;
GO

IF OBJECT_ID(N'dbo.SupHotelInfo', N'U') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
          AND name = N'IX_SupHotelInfo_RecallGridId'
    )
        DROP INDEX IX_SupHotelInfo_RecallGridId ON dbo.SupHotelInfo;

    CREATE NONCLUSTERED INDEX IX_SupHotelInfo_RecallGridId
    ON dbo.SupHotelInfo(RecallGridId ASC, SupHotelId ASC)
    INCLUDE
    (
        SupId,
        SHotelCode,
        MTHotelCode,
        HotelName,
        HotelNameEn,
        Longitude,
        Latitude,
        StarLevel,
        Address,
        RegionLevel1Code,
        CountryCode,
        CountryName,
        CityCode,
        CityName,
        PostCode,
        Tel,
        Fax,
        Email,
        CoordType,
        RegionLevel1Name,
        RegionLevel2Code,
        RegionLevel2Name,
        RegionLevel3Code,
        RegionLevel3Name,
        RegionResolveStatus,
        DataQualityStatus,
        DataQualityFlags
    )
    WHERE State = 1;
END;
GO

IF OBJECT_ID(N'dbo.OTAHotelInfo', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.OTAHotelInfo')
         AND name = N'IX_OTAHotelInfo_RecallGridId'
   )
BEGIN
    UPDATE STATISTICS dbo.OTAHotelInfo IX_OTAHotelInfo_RecallGridId WITH FULLSCAN;
END;
GO

IF OBJECT_ID(N'dbo.SupHotelInfo', N'U') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.SupHotelInfo')
         AND name = N'IX_SupHotelInfo_RecallGridId'
   )
BEGIN
    UPDATE STATISTICS dbo.SupHotelInfo IX_SupHotelInfo_RecallGridId WITH FULLSCAN;
END;
GO

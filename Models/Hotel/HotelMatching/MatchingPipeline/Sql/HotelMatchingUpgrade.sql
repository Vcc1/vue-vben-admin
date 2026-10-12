/*
  Hotel MatchingPipeline - upgrade existing raw and OTA clean tables.
  Run before HotelMatchingCreate.sql. This script is idempotent.
  The application never executes it.
*/

IF COL_LENGTH('dbo.OTAHotelInfoRaw', 'DataVersion') IS NULL
    ALTER TABLE dbo.OTAHotelInfoRaw ADD DataVersion bigint NOT NULL CONSTRAINT DF_OTAHotelInfoRaw_DataVersion DEFAULT (1);
IF COL_LENGTH('dbo.OTAHotelInfoRaw', 'DataHash') IS NULL
    ALTER TABLE dbo.OTAHotelInfoRaw ADD DataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.OTAHotelInfoRaw', 'DataChangeTime') IS NULL
    ALTER TABLE dbo.OTAHotelInfoRaw ADD DataChangeTime datetime2(3) NOT NULL CONSTRAINT DF_OTAHotelInfoRaw_DataChangeTime DEFAULT (sysdatetime());
IF COL_LENGTH('dbo.SupHotelInfoRaw', 'DataVersion') IS NULL
    ALTER TABLE dbo.SupHotelInfoRaw ADD DataVersion bigint NOT NULL CONSTRAINT DF_SupHotelInfoRaw_DataVersion DEFAULT (1);
IF COL_LENGTH('dbo.SupHotelInfoRaw', 'DataHash') IS NULL
    ALTER TABLE dbo.SupHotelInfoRaw ADD DataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.SupHotelInfoRaw', 'DataChangeTime') IS NULL
    ALTER TABLE dbo.SupHotelInfoRaw ADD DataChangeTime datetime2(3) NOT NULL CONSTRAINT DF_SupHotelInfoRaw_DataChangeTime DEFAULT (sysdatetime());

IF OBJECT_ID('dbo.HotelStarLevelIssue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HotelStarLevelIssue
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_HotelStarLevelIssue PRIMARY KEY,
        SourceType tinyint NOT NULL,
        SupplierId int NOT NULL,
        SourceId int NOT NULL,
        RawStarLevel nvarchar(100) COLLATE Chinese_PRC_BIN2 NOT NULL,
        FailureReason varchar(100) NOT NULL,
        FirstSeenTime datetime2(3) NOT NULL CONSTRAINT DF_HotelStarLevelIssue_FirstSeenTime DEFAULT (sysdatetime()),
        CONSTRAINT UQ_HotelStarLevelIssue_SourceSupplierRaw UNIQUE (SourceType, SupplierId, RawStarLevel)
    );
END;

IF COL_LENGTH('dbo.OTAHotelInfo', 'SourceDataVersion') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD SourceDataVersion bigint NOT NULL CONSTRAINT DF_OTAHotelInfo_SourceDataVersion DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'SourceDataHash') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD SourceDataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'NormalizedDataHash') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD NormalizedDataHash varbinary(32) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel1Code') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel1Code varchar(50) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel1Name') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel1Name nvarchar(100) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel2Code') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel2Code varchar(50) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel2Name') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel2Name nvarchar(100) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel3Code') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel3Code varchar(50) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionLevel3Name') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionLevel3Name nvarchar(100) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionResolveStatus') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionResolveStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_RegionResolveStatus DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'RegionFenceVersion') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD RegionFenceVersion int NOT NULL CONSTRAINT DF_OTAHotelInfo_RegionFenceVersion DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'CleanStatus') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD CleanStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_CleanStatus DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'CleanError') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD CleanError nvarchar(1000) NULL;
IF COL_LENGTH('dbo.OTAHotelInfo', 'DataQualityStatus') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD DataQualityStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_DataQualityStatus DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'DataQualityFlags') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD DataQualityFlags bigint NOT NULL CONSTRAINT DF_OTAHotelInfo_DataQualityFlags DEFAULT (0);
IF COL_LENGTH('dbo.OTAHotelInfo', 'DataQualityDetailsJson') IS NULL
    ALTER TABLE dbo.OTAHotelInfo ADD DataQualityDetailsJson nvarchar(max) NULL;

IF OBJECT_ID('dbo.SupHotelInfo', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.SupHotelInfo', 'CountryCode') IS NULL
        ALTER TABLE dbo.SupHotelInfo ADD CountryCode nvarchar(50) NULL;
    IF COL_LENGTH('dbo.SupHotelInfo', 'CountryName') IS NULL
        ALTER TABLE dbo.SupHotelInfo ADD CountryName nvarchar(50) NULL;
    IF COL_LENGTH('dbo.SupHotelInfo', 'CityCode') IS NULL
        ALTER TABLE dbo.SupHotelInfo ADD CityCode nvarchar(50) NULL;
    IF COL_LENGTH('dbo.SupHotelInfo', 'CityName') IS NULL
        ALTER TABLE dbo.SupHotelInfo ADD CityName nvarchar(100) NULL;
END;

-- Remove the earlier JSON phone draft. Phone rows are created by HotelMatchingCreate.sql.
IF COL_LENGTH('dbo.OTAHotelInfo', 'NormalizedPhonesJson') IS NOT NULL
    ALTER TABLE dbo.OTAHotelInfo DROP COLUMN NormalizedPhonesJson;
IF COL_LENGTH('dbo.SupHotelInfo', 'NormalizedPhonesJson') IS NOT NULL
    ALTER TABLE dbo.SupHotelInfo DROP COLUMN NormalizedPhonesJson;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfoRaw_DataChangeTime' AND object_id = OBJECT_ID('dbo.OTAHotelInfoRaw'))
    CREATE INDEX IX_OTAHotelInfoRaw_DataChangeTime ON dbo.OTAHotelInfoRaw(DataChangeTime, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfoRaw_DataVersion' AND object_id = OBJECT_ID('dbo.OTAHotelInfoRaw'))
    CREATE INDEX IX_OTAHotelInfoRaw_DataVersion ON dbo.OTAHotelInfoRaw(DataVersion, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfoRaw_DataChangeTime' AND object_id = OBJECT_ID('dbo.SupHotelInfoRaw'))
    CREATE INDEX IX_SupHotelInfoRaw_DataChangeTime ON dbo.SupHotelInfoRaw(DataChangeTime, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupHotelInfoRaw_DataVersion' AND object_id = OBJECT_ID('dbo.SupHotelInfoRaw'))
    CREATE INDEX IX_SupHotelInfoRaw_DataVersion ON dbo.SupHotelInfoRaw(DataVersion, Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OTAHotelInfo_RawId' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE UNIQUE INDEX UX_OTAHotelInfo_RawId ON dbo.OTAHotelInfo(RawId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfo_MTHotelCode' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE INDEX IX_OTAHotelInfo_MTHotelCode ON dbo.OTAHotelInfo(MTHotelCode);
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OTAHotelInfo_Clean' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    DROP INDEX IX_OTAHotelInfo_Clean ON dbo.OTAHotelInfo;
CREATE INDEX IX_OTAHotelInfo_Clean ON dbo.OTAHotelInfo(Estate, CleanStatus, DataQualityStatus, CleanVersion);

IF OBJECT_ID('dbo.TR_OTAHotelInfo_SetGeoPoint', 'TR') IS NULL
    EXEC('CREATE TRIGGER dbo.TR_OTAHotelInfo_SetGeoPoint ON dbo.OTAHotelInfo AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; UPDATE target SET GeoPoint = CASE WHEN target.Latitude IS NULL OR target.Longitude IS NULL THEN NULL ELSE geography::Point(target.Latitude, target.Longitude, 4326) END FROM dbo.OTAHotelInfo target INNER JOIN inserted item ON item.Id = target.Id; END');

IF NOT EXISTS (SELECT 1 FROM sys.spatial_indexes WHERE name = 'SIX_OTAHotelInfo_GeoPoint' AND object_id = OBJECT_ID('dbo.OTAHotelInfo'))
    CREATE SPATIAL INDEX SIX_OTAHotelInfo_GeoPoint ON dbo.OTAHotelInfo(GeoPoint) USING GEOGRAPHY_AUTO_GRID;

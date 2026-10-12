IF OBJECT_ID(N'dbo.OTAHotelInfoRaw', N'U') IS NULL
    THROW 50001, 'Create dbo.OTAHotelInfoRaw before dbo.OTAHotelInfo.', 1;
GO

IF OBJECT_ID(N'dbo.OTAHotelInfo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OTAHotelInfo
    (
        HotelID int NOT NULL,
        MTHotelCode varchar(100) COLLATE Chinese_PRC_CI_AS NOT NULL,
        HotelName nvarchar(200) COLLATE Chinese_PRC_CI_AS NOT NULL,
        HotelEnName nvarchar(300) COLLATE Chinese_PRC_CI_AS NULL,
        CityCode varchar(90) COLLATE Chinese_PRC_CI_AS NULL,
        Address nvarchar(300) COLLATE Chinese_PRC_CI_AS NULL,
        EnAddress nvarchar(500) COLLATE Chinese_PRC_CI_AS NULL,
        Estate int NOT NULL,
        Flag tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_Flag DEFAULT (0),
        AreaID int NOT NULL,
        CountryID int NOT NULL CONSTRAINT DF_OTAHotelInfo_CountryID DEFAULT (0),
        ProvinceID int NOT NULL CONSTRAINT DF_OTAHotelInfo_ProvinceID DEFAULT (0),
        RootCityID int NOT NULL CONSTRAINT DF_OTAHotelInfo_RootCityID DEFAULT (0),
        CityID int NOT NULL CONSTRAINT DF_OTAHotelInfo_CityID DEFAULT (0),
        CountryName varchar(64) COLLATE Chinese_PRC_CI_AS NULL,
        CountryCode varchar(128) COLLATE Chinese_PRC_CI_AS NULL,
        CountryNameEn nvarchar(100) COLLATE Chinese_PRC_CI_AS NULL,
        CityName varchar(64) COLLATE Chinese_PRC_CI_AS NULL,
        CityNameEn nvarchar(100) COLLATE Chinese_PRC_CI_AS NULL,
        Tel nvarchar(60) COLLATE Chinese_PRC_CI_AS NULL,
        Fax nvarchar(60) COLLATE Chinese_PRC_CI_AS NULL,
        PostCode varchar(50) COLLATE Chinese_PRC_CI_AS NULL,
        Email varchar(100) COLLATE Chinese_PRC_CI_AS NULL,
        WebSite varchar(500) COLLATE Chinese_PRC_CI_AS NULL,
        StarLevel decimal(3,1) NULL,
        CoordType tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_CoordType DEFAULT (0),
        Longitude decimal(10,7) NULL,
        Latitude decimal(10,7) NULL,
        GeoPoint geography NULL,
        SourceLastModifyTime datetime NULL,
        CleanVersion int NOT NULL,
        CleanTime datetime NOT NULL CONSTRAINT DF_OTAHotelInfo_CleanTime DEFAULT (getdate()),
        CreateTime datetime NOT NULL CONSTRAINT DF_OTAHotelInfo_CreateTime DEFAULT (getdate()),
        UpdateTime datetime NOT NULL CONSTRAINT DF_OTAHotelInfo_UpdateTime DEFAULT (getdate()),
        SourceDataVersion bigint NOT NULL CONSTRAINT DF_OTAHotelInfo_SourceDataVersion DEFAULT (0),
        SourceDataHash varbinary(32) NULL,
        NormalizedDataHash varbinary(32) NULL,
        RegionLevel1Code varchar(50) COLLATE Chinese_PRC_CI_AS NULL,
        RegionLevel1Name nvarchar(100) COLLATE Chinese_PRC_CI_AS NULL,
        RegionLevel2Code varchar(50) COLLATE Chinese_PRC_CI_AS NULL,
        RegionLevel2Name nvarchar(100) COLLATE Chinese_PRC_CI_AS NULL,
        RegionLevel3Code varchar(50) COLLATE Chinese_PRC_CI_AS NULL,
        RegionLevel3Name nvarchar(100) COLLATE Chinese_PRC_CI_AS NULL,
        RegionResolveStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_RegionResolveStatus DEFAULT (0),
        RegionFenceVersion int NOT NULL CONSTRAINT DF_OTAHotelInfo_RegionFenceVersion DEFAULT (0),
        CleanStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_CleanStatus DEFAULT (0),
        CleanError nvarchar(1000) COLLATE Chinese_PRC_CI_AS NULL,
        DataQualityStatus tinyint NOT NULL CONSTRAINT DF_OTAHotelInfo_DataQualityStatus DEFAULT (0),
        DataQualityFlags bigint NOT NULL CONSTRAINT DF_OTAHotelInfo_DataQualityFlags DEFAULT (0),
        DataQualityDetailsJson nvarchar(max) COLLATE Chinese_PRC_CI_AS NULL,
        CONSTRAINT PK_OTAHotelInfo PRIMARY KEY CLUSTERED (HotelID) ON [CACHE],
        CONSTRAINT FK_OTAHotelInfo_OTAHotelInfoRaw
            FOREIGN KEY (HotelID) REFERENCES dbo.OTAHotelInfoRaw(HotelID),
        CONSTRAINT CK_OTAHotelInfo_CoordType CHECK (CoordType IN (0, 1, 2, 3)),
        CONSTRAINT CK_OTAHotelInfo_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_OTAHotelInfo_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_OTAHotelInfo_CoordinatePair CHECK
        (
            (Longitude IS NULL AND Latitude IS NULL)
            OR (Longitude IS NOT NULL AND Latitude IS NOT NULL)
        )
    ) ON [CACHE] TEXTIMAGE_ON [CACHE];
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OTAHotelInfo')
      AND name = N'IX_OTAHotelInfo_MTHotelCode'
)
    CREATE INDEX IX_OTAHotelInfo_MTHotelCode
        ON dbo.OTAHotelInfo(MTHotelCode) ON [CACHE];
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OTAHotelInfo')
      AND name = N'IX_OTAHotelInfo_Clean'
)
    CREATE INDEX IX_OTAHotelInfo_Clean
        ON dbo.OTAHotelInfo(Estate, CleanStatus, DataQualityStatus, CleanVersion) ON [CACHE];
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.spatial_indexes
    WHERE object_id = OBJECT_ID(N'dbo.OTAHotelInfo')
      AND name = N'SIX_OTAHotelInfo_GeoPoint'
)
    CREATE SPATIAL INDEX SIX_OTAHotelInfo_GeoPoint
        ON dbo.OTAHotelInfo(GeoPoint)
        USING GEOGRAPHY_AUTO_GRID
        WITH (CELLS_PER_OBJECT = 12)
        ON [CACHE];
GO

CREATE OR ALTER TRIGGER dbo.TR_OTAHotelInfo_SetGeoPoint
ON dbo.OTAHotelInfo
WITH EXECUTE AS CALLER
FOR INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE target
    SET GeoPoint = CASE
        WHEN target.Latitude IS NULL OR target.Longitude IS NULL THEN NULL
        ELSE geography::Point(target.Latitude, target.Longitude, 4326)
    END
    FROM dbo.OTAHotelInfo AS target
    INNER JOIN inserted AS item ON item.HotelID = target.HotelID;
END;
GO
